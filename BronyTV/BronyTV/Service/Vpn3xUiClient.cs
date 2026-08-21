using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

/// <summary>
/// Тонкий HTTP-клиент панели 3X-UI (x-ui). Умеет логиниться и держит
/// сессионную куку, находит нужный инбаунд, создаёт/продлевает клиентов
/// и удаляет их. Используется только для реального предоставления доступа.
/// </summary>
public interface IVpn3xUiClient
{
    /// <summary>Настроена ли панель (webhook-интеграция активна и все параметры заполнены).</summary>
    bool IsConfigured { get; }

    /// <summary>Создаёт или продлевает клиента с заданным UUID до <paramref name="expiresAtUtc"/>.</summary>
    Task<bool> UpsertClientAsync(
        string clientUuid,
        string email,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Проверяет наличие клиента с заданным UUID на панели.</summary>
    Task<bool> ClientExistsAsync(string clientUuid, CancellationToken cancellationToken = default);

    /// <summary>Полностью удаляет клиента с панели (например, при отключении подписки).</summary>
    Task<bool> RemoveClientAsync(string clientUuid, CancellationToken cancellationToken = default);

    /// <summary>Принудительно отключает клиентов, срок действия которых истёк.</summary>
    Task<int> DisableExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>Внутренняя модель клиента из настроек инбаунда.</summary>
internal sealed class XuiClientEntry
{
    public string Id { get; set; } = string.Empty;
    public long? ExpiryTime { get; set; }
    public string? Email { get; set; }
}

internal sealed class XuiInbound
{
    public long Id { get; set; }
    public string? Protocol { get; set; }
    public string? Settings { get; set; }
}

/// <inheritdoc cref="IVpn3xUiClient"/>
public partial class Vpn3xUiClient : IVpn3xUiClient
{
    private readonly IOptions<VpnOptions> _options;
    private readonly ILogger<Vpn3xUiClient> _logger;
    private readonly HttpClient _http;
    private readonly object _sync = new object();
    private DateTime _cookieExpiresUtc;
    private string? _cookie;

    public Vpn3xUiClient(
        IOptions<VpnOptions> options,
        ILogger<Vpn3xUiClient> logger,
        HttpClient http)
    {
        _options = options;
        _logger = logger;
        _http = http;
    }

    private VpnOptions Options => _options.Value;

    public bool IsConfigured => Options.Enabled
        && !string.IsNullOrWhiteSpace(Options.PanelApiUrl)
        && !string.IsNullOrWhiteSpace(Options.PanelUsername)
        && !string.IsNullOrWhiteSpace(Options.PanelPassword);

    private string ApiBase
    {
        get
        {
            var url = Options.PanelApiUrl?.Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url;
        }
    }

    public Task<bool> UpsertClientAsync(
        string clientUuid,
        string email,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientUuid))
        {
            return Task.FromResult(false);
        }

        if (!IsConfigured)
        {
            // Панель не настроена — пропускаем реальное провижионирование.
            _logger.LogInformation("3X-UI не сконфигурирован: клиент {Uuid} (email {Email}) не создавался.", clientUuid, email);
            return Task.FromResult(false);
        }

        try
        {
            return UpsertClientCoreAsync(clientUuid, email, expiresAtUtc, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при создании/продлении клиента {Uuid}.", clientUuid);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> RemoveClientAsync(string clientUuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientUuid) || !IsConfigured)
        {
            return false;
        }

        try
        {
            var inboundId = await FindInboundIdAsync(cancellationToken).ConfigureAwait(false);
            if (!inboundId.HasValue)
            {
                return false;
            }

            var url = $"{ApiBase}/inbounds/{inboundId.Value}/{WebUtility.UrlEncode(clientUuid)}/delClient";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            await EnsureCookieAsync(request, cancellationToken).ConfigureAwait(false);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("3X-UI вернул {Status} при удалении клиента {Uuid}.", response.StatusCode, clientUuid);
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var success = IsSuccessPayload(body);
            _logger.LogInformation("3X-UI удаление клиента {Uuid}: {Ok}", clientUuid, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при удалении клиента {Uuid}.", clientUuid);
            return false;
        }
    }

    public async Task<bool> ClientExistsAsync(
        string clientUuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientUuid) || !IsConfigured)
        {
            return false;
        }

        try
        {
            var inboundId = await FindInboundIdAsync(cancellationToken).ConfigureAwait(false);
            if (!inboundId.HasValue)
            {
                return false;
            }

            var inbounds = await ListInboundsAsync(cancellationToken).ConfigureAwait(false);
            var inbound = inbounds.FirstOrDefault(i => i != null && i.Id == inboundId.Value);
            if (inbound == null)
            {
                return false;
            }

            return ParseClients(inbound).Any(c => string.Equals(c.Id, clientUuid, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при проверке существования клиента {Uuid}.", clientUuid);
            return false;
        }
    }

    public async Task<int> DisableExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return 0;
        }

        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var removed = 0;
            var inbounds = await ListInboundsAsync(cancellationToken).ConfigureAwait(false);

            foreach (var inbound in inbounds.Where(i => i != null))
            {
                var clients = ParseClients(inbound!);
                foreach (var client in clients)
                {
                    // При expiryTime <= 0 клиент считается бессрочным — не трогаем.
                    if (client.ExpiryTime <= 0)
                    {
                        continue;
                    }
                    if (client.ExpiryTime >= nowMs)
                    {
                        continue;
                    }

                    _logger.LogInformation("3X-UI: срок действия клиента {Uuid}|{Email} истёк, удаляю.", client.Id, client.Email);
                    await RemoveClientAsync(client.Id, cancellationToken).ConfigureAwait(false);
                    removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при очистке просроченных клиентов.");
            return 0;
        }
    }

    private async Task<bool> UpsertClientCoreAsync(
        string clientUuid,
        string email,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var inboundId = await FindInboundIdAsync(cancellationToken).ConfigureAwait(false);
        if (!inboundId.HasValue)
        {
            _logger.LogWarning("3X-UI: инбаунд не найден, клиент {Uuid} не создан.", clientUuid);
            return false;
        }

        var payload = JsonSerializer.Serialize(new
        {
            id = clientUuid,
            email,
            limitIp = 0,
            totalGB = 0,
            expiryTime = ToUnixTimeMs(expiresAtUtc),
            enable = true,
            tgId = string.Empty,
            subId = clientUuid,
            reset = 0
        });

        // Пробуем сначала обновить, если клиент уже существует; иначе создаём.
        var updateUrl = $"{ApiBase}/inbounds/{inboundId.Value}/updateClient/{WebUtility.UrlEncode(clientUuid)}";
        using (var updateRequest = new HttpRequestMessage(HttpMethod.Post, updateUrl))
        {
            updateRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            await EnsureCookieAsync(updateRequest, cancellationToken).ConfigureAwait(false);
            var updateResponse = await _http.SendAsync(updateRequest, cancellationToken).ConfigureAwait(false);
            var updateBody = updateResponse.IsSuccessStatusCode
                ? await updateResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : string.Empty;
            if (updateResponse.IsSuccessStatusCode && IsSuccessPayload(updateBody))
            {
                _logger.LogInformation("3X-UI клиент {Uuid} продлён до {Expires}.", clientUuid, expiresAtUtc);
                return true;
            }
        }

        var addUrl = $"{ApiBase}/inbounds/{inboundId.Value}/addClient";
        using (var addRequest = new HttpRequestMessage(HttpMethod.Post, addUrl))
        {
            addRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            await EnsureCookieAsync(addRequest, cancellationToken).ConfigureAwait(false);
            var addResponse = await _http.SendAsync(addRequest, cancellationToken).ConfigureAwait(false);
            var addBody = await addResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var ok = addResponse.IsSuccessStatusCode && IsSuccessPayload(addBody);
            _logger.LogInformation("3X-UI клиент {Uuid} создан: {Ok}.", clientUuid, ok);
            return ok;
        }
    }

    private async Task<long?> FindInboundIdAsync(CancellationToken cancellationToken)
    {
        if (Options.PanelInboundId.HasValue && Options.PanelInboundId.Value > 0)
        {
            return Options.PanelInboundId.Value;
        }

        var inbounds = await ListInboundsAsync(cancellationToken).ConfigureAwait(false);
        // Отдаём предпочтение VLESS-инбаунду.
        return inbounds.FirstOrDefault(i => i != null && string.Equals(i.Protocol, "vless", StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private async Task<IReadOnlyList<XuiInbound?>> ListInboundsAsync(CancellationToken cancellationToken)
    {
        var url = $"{ApiBase}/inbounds/list";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await EnsureCookieAsync(request, cancellationToken).ConfigureAwait(false);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<XuiInbound?>();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!IsSuccessPayload(body))
        {
            return Array.Empty<XuiInbound?>();
        }

        var doc = System.Text.Json.Nodes.JsonNode.Parse(body);
        var arr = doc?["obj"]?.AsArray();
        if (arr == null)
        {
            return Array.Empty<XuiInbound?>();
        }

        var result = new List<XuiInbound?>();
        foreach (var node in arr)
        {
            if (node == null)
            {
                continue;
            }
            result.Add(new XuiInbound
            {
                Id = node["id"]?.GetValue<long>() ?? 0,
                Protocol = node["protocol"]?.GetValue<string>(),
                Settings = node["settings"]?.GetValue<string>()
            });
        }
        return result;
    }

    private static List<XuiClientEntry> ParseClients(XuiInbound inbound)
    {
        var result = new List<XuiClientEntry>();
        if (string.IsNullOrWhiteSpace(inbound.Settings))
        {
            return result;
        }

        try
        {
            var doc = System.Text.Json.Nodes.JsonNode.Parse(inbound.Settings!);
            var clients = doc?["clients"]?.AsArray();
            if (clients == null)
            {
                return result;
            }
            foreach (var node in clients)
            {
                if (node == null)
                {
                    continue;
                }
                result.Add(new XuiClientEntry
                {
                    Id = node["id"]?.GetValue<string>() ?? string.Empty,
                    ExpiryTime = node["expiryTime"]?.GetValue<long>() ?? 0,
                    Email = node["email"]?.GetValue<string>()
                });
            }
        }
        catch (Exception)
        {
            // Некорректный settings — пропускаем.
        }

        return result;
    }

    private static bool IsSuccessPayload(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }
        try
        {
            var doc = System.Text.Json.Nodes.JsonNode.Parse(body);
            var success = doc?["success"]?.GetValue<bool>() ?? false;
            return success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static long ToUnixTimeMs(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }

    private async Task EnsureCookieAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool needLogin;
        lock (_sync)
        {
            needLogin = string.IsNullOrEmpty(_cookie) || DateTime.UtcNow >= _cookieExpiresUtc;
        }

        if (needLogin)
        {
            await LoginAsync().ConfigureAwait(false);
        }

        // Сама кука отправляется автоматически внутренним CookieContainer клиента.
    }

    private async Task<string?> LoginAsync()
    {
        var url = $"{ApiBase}/login";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = Options.PanelUsername ?? string.Empty,
                ["password"] = Options.PanelPassword ?? string.Empty
            })
        };

        using var response = await _http.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("3X-UI логин не удался: {Status}.", response.StatusCode);
            return null;
        }

        var cookieHeader = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? string.Join(";", setCookies)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            // Кука уже сохранена внутренним CookieContainer клиента; просто помечаем сессию активной.
            // Последующие запросы отправят куку автоматически.
            cookieHeader = "bronytv-xui-session=1";
        }

        lock (_sync)
        {
            _cookie = cookieHeader;
            _cookieExpiresUtc = DateTime.UtcNow.AddHours(5);
        }
        return _cookie;
    }
}
