using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

/// <summary>
/// HTTP-клиент панели 3X-UI (x-ui) v3.x. Авторизуется по API-токену (Bearer)
/// и управляет клиентами напрямую через роуты <c>/panel/api/clients/*</c>
/// (клиенты — сущности первого класса, а не часть настроек инбаунда).
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

/// <summary>Модель клиента 3X-UI v3.x из ответа <c>/panel/api/clients/list</c>.</summary>
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
    /// Нормализует базовый URL панели до <c>{base}/panel</c>, независимо от того,
    /// передан ли <c>VPN_PANEL_API_URL</c> уже с суффиксом <c>/panel</c> или без него.
    /// </summary>
    private string ApiPanelBase
    {
        get
        {
            var baseUrl = ApiBase;
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            const string panelSuffix = "/panel";
            if (baseUrl.EndsWith(panelSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = baseUrl[..^panelSuffix.Length].TrimEnd('/');
            }

            return $"{baseUrl}/panel";
        }
    }

    /// <summary>
    /// Базовый путь API клиентов 3X-UI v3.x: <c>{base}/panel/api/clients</c>.
    /// Клиенты — сущности первого класса и управляются напрямую этими роутами.
    /// </summary>
    private string ApiClientsBase => $"{ApiPanelBase}/api/clients";

    /// <summary>
    /// Базовый путь API инбаундов: <c>{base}/panel/api/inbounds</c>.
    /// Нужен только чтобы найти ID инбаунда для назначения нового клиента.
    /// </summary>
    private string ApiInboundsBase => $"{ApiPanelBase}/api/inbounds";

    /// <summary>
    /// Подписывает запрос Bearer-токеном из конфигурации (VPN_PANEL_API_TOKEN).
    /// Все запросы к <c>{base}/panel/api/...</c> отправляются с заголовком
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
            var email = await ResolveEmailByUuidAsync(clientUuid, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var url = $"{ApiClientsBase}/del/{WebUtility.UrlEncode(email)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            Authorize(request);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("3X-UI вернул {Status} при удалении клиента {Email}.", response.StatusCode, email);
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var success = IsSuccessPayload(body);
            _logger.LogInformation("3X-UI удаление клиента {Email}: {Ok}", email, success);
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
            var email = await ResolveEmailByUuidAsync(clientUuid, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            // GET /panel/api/clients/get/{email} -> HTTP 200 означает, что клиент существует.
            var url = $"{ApiClientsBase}/get/{WebUtility.UrlEncode(email)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            Authorize(request);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Дополнительно проверяем флаг success в теле, если он присутствует.
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return IsSuccessPayload(body);
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
            var clients = await ListClientsAsync(cancellationToken).ConfigureAwait(false);

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

                var email = client.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    continue;
                }

                _logger.LogInformation("3X-UI: срок действия клиента {Email} истёк, удаляю.", email);
                await DeleteClientByEmailAsync(email, cancellationToken).ConfigureAwait(false);
                removed++;
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
        var expiryTimestampMs = ToUnixTimeMs(expiresAtUtc);

        // Сначала пробуем обновить по email: POST /panel/api/clients/update/{email}.
        var updateUrl = $"{ApiClientsBase}/update/{WebUtility.UrlEncode(email)}";
        using (var updateRequest = new HttpRequestMessage(HttpMethod.Post, updateUrl))
        {
            updateRequest.Content = new StringContent(
                BuildUpdatePayload(clientUuid, email, expiryTimestampMs),
                Encoding.UTF8,
                "application/json");
            Authorize(updateRequest);

            using var updateResponse = await _http.SendAsync(updateRequest, cancellationToken).ConfigureAwait(false);
            if (updateResponse.IsSuccessStatusCode)
            {
                var updateBody = await updateResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (IsSuccessPayload(updateBody))
                {
                    _logger.LogInformation("3X-UI клиент {Email} продлён до {Expires}.", email, expiresAtUtc);
                    return true;
                }
            }
        }

        // Если клиент не существовал — создаём: POST /panel/api/clients/add.
        var inboundId = await FindInboundIdAsync(cancellationToken).ConfigureAwait(false);
        if (!inboundId.HasValue)
        {
            _logger.LogWarning("3X-UI: инбаунд не найден, клиент {Email} не создан.", email);
            return false;
        }

        var addUrl = $"{ApiClientsBase}/add";
        using (var addRequest = new HttpRequestMessage(HttpMethod.Post, addUrl))
        {
            addRequest.Content = new StringContent(
                BuildAddPayload(inboundId.Value, clientUuid, email, expiryTimestampMs),
                Encoding.UTF8,
                "application/json");
            Authorize(addRequest);

            using var addResponse = await _http.SendAsync(addRequest, cancellationToken).ConfigureAwait(false);
            var addBody = await addResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var ok = addResponse.IsSuccessStatusCode && IsSuccessPayload(addBody);
            _logger.LogInformation("3X-UI клиент {Email} создан: {Ok}.", email, ok);
            return ok;
        }
    }

    /// <summary>
    /// Тело запроса на создание клиента:
    /// <c>{"client": {id, email, enable, expiryTime, flow}, "inboundIds": [inboundId]}</c>.
    /// </summary>
    private static string BuildAddPayload(long inboundId, string clientUuid, string email, long expiryTimestampMs)
    {
        var client = BuildClientBody(clientUuid, email, expiryTimestampMs);
        return "{\"client\":" + client + ",\"inboundIds\":[" + inboundId + "]}";
    }

    /// <summary>
    /// Тело запроса на обновление клиента: <c>{"client": {id, email, enable, expiryTime, flow}}</c>.
    /// Формат объекта client такой же, как при создании.
    /// </summary>
    private static string BuildUpdatePayload(string clientUuid, string email, long expiryTimestampMs)
    {
        var client = BuildClientBody(clientUuid, email, expiryTimestampMs);
        return "{\"client\":" + client + "}";
    }

    private static string BuildClientBody(string clientUuid, string email, long expiryTimestampMs)
    {
        return "{\"id\":\"" + clientUuid
               + "\",\"email\":\"" + email
               + "\",\"enable\":true"
               + ",\"expiryTime\":" + expiryTimestampMs
               + ",\"flow\":\"\"}";
    }

    /// <summary>
    /// Ищет ID инбаунда: из конфигурации <c>PanelInboundId</c> либо первый VLESS-инбаунд.
    /// </summary>
    private async Task<long?> FindInboundIdAsync(CancellationToken cancellationToken)
    {
        if (Options.PanelInboundId.HasValue && Options.PanelInboundId.Value > 0)
        {
            return Options.PanelInboundId.Value;
        }

        var inbounds = await ListInboundsAsync(cancellationToken).ConfigureAwait(false);
        return inbounds.FirstOrDefault(
            i => i != null && string.Equals(i.Protocol, "vless", StringComparison.OrdinalIgnoreCase)
        )?.Id;
    }

    /// <summary>
    /// Возвращает email клиента по его UUID (id) через список <c>/panel/api/clients/list</c>.
    /// </summary>
    private async Task<string?> ResolveEmailByUuidAsync(string clientUuid, CancellationToken cancellationToken)
    {
        var clients = await ListClientsAsync(cancellationToken).ConfigureAwait(false);
        var match = clients.FirstOrDefault(
            c => string.Equals(c.Id, clientUuid, StringComparison.OrdinalIgnoreCase)
        );
        return match?.Email;
    }

    private async Task<bool> DeleteClientByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var url = $"{ApiClientsBase}/del/{WebUtility.UrlEncode(email)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("3X-UI вернул {Status} при удалении клиента {Email}.", response.StatusCode, email);
            return false;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return IsSuccessPayload(body);
    }

    /// <summary>
    /// Получает список клиентов: GET <c>/panel/api/clients/list</c>.
    /// </summary>
    private async Task<IReadOnlyList<XuiClientEntry>> ListClientsAsync(CancellationToken cancellationToken)
    {
        var url = $"{ApiClientsBase}/list";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<XuiClientEntry>();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!IsSuccessPayload(body))
        {
            return Array.Empty<XuiClientEntry>();
        }

        return ParseClientList(body);
    }

    /// <summary>
    /// Разбирает ответ <c>GET /panel/api/clients/list</c>.
    /// Ожидается структура <c>{success: true, obj: [{id, email, enable, expiryTime, ...}]}</c>.
    /// </summary>
    private static IReadOnlyList<XuiClientEntry> ParseClientList(string body)
    {
        var result = new List<XuiClientEntry>();
        try
        {
            var doc = JsonNode.Parse(body);
            var arr = doc?["obj"]?.AsArray();
            if (arr == null)
            {
                return result;
            }

            foreach (var node in arr)
            {
                if (node == null)
                {
                    continue;
                }
                result.Add(new XuiClientEntry
                {
                    Id = node["id"]?.GetValue<string>() ?? string.Empty,
                    Email = node["email"]?.GetValue<string>(),
                    ExpiryTime = node["expiryTime"]?.GetValue<long>() ?? 0
                });
            }
        }
        catch (Exception)
        {
            // Некорректный ответ — возвращаем пустой список.
        }

        return result;
    }

    /// <summary>
    /// Получает список инбаундов (для поиска ID по протоколу): GET <c>/panel/api/inbounds/list</c>.
    /// </summary>
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

        var doc = JsonNode.Parse(body);
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
                Protocol = node["protocol"]?.GetValue<string>()
            });
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
            var doc = JsonNode.Parse(body);
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
