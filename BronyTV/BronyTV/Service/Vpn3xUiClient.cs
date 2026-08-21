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
/// HTTP-клиент панели 3X-UI (x-ui). Авторизуется по API-токену (Bearer),
/// находит нужный инбаунд, создаёт/продлевает клиентов и удаляет их.
/// Используется только для реального предоставления доступа.
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
        && !string.IsNullOrWhiteSpace(Options.PanelApiToken);

    private string ApiBase
    {
        get
        {
            var url = Options.PanelApiUrl?.Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url;
        }
    }

    /// <summary>
    /// Базовый путь API инбаундов 3X-UI. Все эндпоинты инбаундов живут строго
    /// по префиксу <c>/panel/api/inbounds/...</c>. Нормализуем базовый URL так,
    /// чтобы префикс присутствовал всегда, независимо от того, заканчивается ли
    /// <c>VPN_PANEL_API_URL</c> на <c>/panel</c> или на слэш.
    /// </summary>
    private string ApiInboundsBase
    {
        get
        {
            var baseUrl = ApiBase;
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            // Если в конфигурации базовый URL уже заканчивается на /panel — не дублируем.
            const string panelSuffix = "/panel";
            if (baseUrl.EndsWith(panelSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = baseUrl[..^panelSuffix.Length].TrimEnd('/');
            }

            return $"{baseUrl}/panel/api/inbounds";
        }
    }

    /// <summary>
    /// Подписывает запрос Bearer-токеном из конфигурации (VPN_PANEL_API_TOKEN).
    /// Все запросы к <c>{base}/panel/api/inbounds/...</c> отправляются с заголовком
    /// <c>Authorization: Bearer &lt;token&gt;</c>, без CookieContainer и логина.
    /// </summary>
    private void Authorize(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Options.PanelApiToken);
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

            var url = $"{ApiInboundsBase}/{inboundId.Value}/{WebUtility.UrlEncode(clientUuid)}/delClient";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            Authorize(request);

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

        var expiryTimestampMs = ToUnixTimeMs(expiresAtUtc);

        // Пробуем сначала обновить, если клиент уже существует; иначе создаём.
        var updateUrl = $"{ApiInboundsBase}/{inboundId.Value}/updateClient/{WebUtility.UrlEncode(clientUuid)}";
        var updatePayload = BuildClientPayload(inboundId.Value, clientUuid, email, expiryTimestampMs);
        using (var updateRequest = new HttpRequestMessage(HttpMethod.Post, updateUrl))
        {
            updateRequest.Content = new StringContent(updatePayload, Encoding.UTF8, "application/json");
            Authorize(updateRequest);
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

        var addUrl = $"{ApiInboundsBase}/{inboundId.Value}/addClient";
        var addPayload = BuildClientPayload(inboundId.Value, clientUuid, email, expiryTimestampMs);
        using (var addRequest = new HttpRequestMessage(HttpMethod.Post, addUrl))
        {
            addRequest.Content = new StringContent(addPayload, Encoding.UTF8, "application/json");
            Authorize(addRequest);
            var addResponse = await _http.SendAsync(addRequest, cancellationToken).ConfigureAwait(false);
            var addBody = await addResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var ok = addResponse.IsSuccessStatusCode && IsSuccessPayload(addBody);
            _logger.LogInformation("3X-UI клиент {Uuid} создан: {Ok}.", clientUuid, ok);
            return ok;
        }
    }

    /// <summary>
    /// Собирает тело запроса 3X-UI в требуемом формате:
    /// <c>{"id": inboundId, "settings": "{\"clients\":[{...}]}"}</c>.
    /// <c>settings</c> — это строка, содержащая вложенный JSON (clients-массив),
    /// а не объект. Для gRPC Reality поле <c>flow</c> передаётся пустым.
    /// </summary>
    private static string BuildClientPayload(long inboundId, string clientUuid, string email, long expiryTimestampMs)
    {
        var settings = "{\"clients\":[{\"id\":\"" + clientUuid
                       + "\",\"email\":\"" + email
                       + "\",\"expiryTime\":" + expiryTimestampMs
                       + ",\"enable\":true,\"flow\":\"\"}]}";

        return "{\"id\":" + inboundId + ",\"settings\":\"" + settings + "\"}";
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
        var url = $"{ApiInboundsBase}/list";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        Authorize(request);

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
}
