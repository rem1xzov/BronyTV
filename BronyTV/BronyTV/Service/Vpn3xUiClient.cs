using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

/// <summary>
/// HTTP-клиент панели 3X-UI (классический API, совместимый с оригинальной x-ui).
/// Клиенты управляются как часть настроек инбаунда через роуты
/// <c>{base}/panel/api/inbounds/*</c>. Токен передаётся заголовком
/// <c>Authorization: Bearer &lt;token&gt;</c> (VPN_PANEL_API_TOKEN).
/// Используется только для реального предоставления доступа.
/// </summary>
public interface IVpn3xUiClient
{
    /// <summary>Настроена ли панель (VPN включён и заполнены URL API + Bearer-токен).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Создаёт или продлевает клиента с заданным UUID до <paramref name="expiresAtUtc"/>.
    /// При неудаче (HTTP-ошибка или <c>success=false</c>) выбрасывает исключение,
    /// чтобы генерация «мёртвой» VLESS-ссылки была заблокирована на слое вызова.
    /// </summary>
    Task<bool> UpsertClientAsync(
        string clientUuid,
        string email,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Проверяет наличие клиента с заданным UUID на панели (в инбаундах VLESS).</summary>
    Task<bool> ClientExistsAsync(string clientUuid, CancellationToken cancellationToken = default);

    /// <summary>Полностью удаляет клиента с панели (например, при отключении подписки).</summary>
    Task<bool> RemoveClientAsync(string clientUuid, CancellationToken cancellationToken = default);

    /// <summary>Принудительно отключает клиентов, срок действия которых истёк.</summary>
    Task<int> DisableExpiredAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IVpn3xUiClient"/>
public class Vpn3xUiClient : IVpn3xUiClient
{
    private readonly IOptions<VpnOptions> _options;
    private readonly VpnConfigResolver _vpnConfig;
    private readonly ILogger<Vpn3xUiClient> _logger;
    private readonly HttpClient _http;

    public Vpn3xUiClient(
        IOptions<VpnOptions> options,
        VpnConfigResolver vpnConfig,
        ILogger<Vpn3xUiClient> logger,
        HttpClient http)
    {
        _options = options;
        _vpnConfig = vpnConfig;
        _logger = logger;
        _http = http;
    }

    private VpnOptions Options => _options.Value;

    public bool IsConfigured => Options.Enabled
        && !string.IsNullOrWhiteSpace(_vpnConfig.PanelApiUrl)
        && !string.IsNullOrWhiteSpace(_vpnConfig.PanelApiToken);

    private string ApiBase
    {
        get
        {
            var url = _vpnConfig.PanelApiUrl?.Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url;
        }
    }

    /// <summary>
    /// Базовый URL API панели: <c>{PanelApiUrl}/panel/api</c>. Относительная склейка
    /// без ведущего слэша сохраняет секретный web-префикс панели, например
    /// <c>https://ip:port/TugsFcqj7OslFxFadz/panel/api</c>. Если URL уже содержит
    /// суффикс <c>/panel</c> — он не дублируется.
    /// </summary>
    private string ApiBase_Api
    {
        get
        {
            var baseUrl = ApiBase;
            if (string.IsNullOrEmpty(baseUrl))
            {
                return string.Empty;
            }

            return baseUrl.EndsWith("/panel", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUrl}/api"
                : $"{baseUrl}/panel/api";
        }
    }

    /// <summary>Базовый путь API инбаундов: <c>{base}/panel/api/inbounds</c>.</summary>
    private string ApiInboundsBase => $"{ApiBase_Api}/inbounds";

    /// <summary>
    /// Подписывает запрос Bearer-токеном из конфигурации (VPN_PANEL_API_TOKEN),
    /// как это принято в современных сборках 3X-UI при включённом API-токене.
    /// </summary>
    private void Authorize(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _vpnConfig.PanelApiToken);
    }

    public async Task<bool> UpsertClientAsync(
        string clientUuid,
        string email,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientUuid))
        {
            throw new ArgumentException("UUID клиента не может быть пустым.", nameof(clientUuid));
        }

        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "3X-UI не сконфигурирован: задайте VPN_PANEL_API_URL и VPN_PANEL_API_TOKEN.");
        }

        var inboundId = await ResolveInboundIdAsync(cancellationToken).ConfigureAwait(false);
        if (!inboundId.HasValue)
        {
            var error = "3X-UI: инбаунд VLESS не найден — не удалось назначить клиента.";
            _logger.LogError("{Error}", error);
            throw new InvalidOperationException(error);
        }

        var expiryMs = ToUnixTimeMs(expiresAtUtc);

        // Проверяем, существует ли уже клиент (восстановление/продление без дубликатов).
        var existing = await FindClientAsync(clientUuid, email, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            _logger.LogInformation(
                "3X-UI: клиент {Uuid} уже существует (inbound {InboundId}), обновляю параметры.",
                clientUuid,
                inboundId.Value);
            await UpdateClientInInboundAsync(
                clientUuid,
                email,
                expiryMs,
                existing.SubId,
                existing.Password,
                existing.Auth,
                cancellationToken).ConfigureAwait(false);
            return true;
        }

        await AddClientToInboundAsync(
            inboundId.Value,
            clientUuid,
            email,
            expiryMs,
            cancellationToken).ConfigureAwait(false);
        return true;
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
            var found = await FindClientByUuidAsync(clientUuid, cancellationToken).ConfigureAwait(false);
            return found != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при проверке существования клиента {Uuid}.", clientUuid);
            return false;
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
            var client = await FindClientByUuidAsync(clientUuid, cancellationToken).ConfigureAwait(false);
            if (client == null)
            {
                return true; // уже отсутствует на панели.
            }

            await DeleteClientFromInboundAsync(
                clientUuid,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при удалении клиента {Uuid}.", clientUuid);
            return false;
        }
    }

    public async Task<int> DisableExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return 0;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var removed = 0;
        try
        {
            var inbounds = await ListInboundIdsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var inboundId in inbounds)
            {
                var clients = await LoadInboundClientsAsync(inboundId, cancellationToken).ConfigureAwait(false);
                foreach (var client in clients)
                {
                    if (client.ExpiryTime <= 0 || client.ExpiryTime >= nowMs)
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "3X-UI: срок действия клиента {Email} истёк (inbound {InboundId}), удаляю.",
                        client.Email ?? client.Id,
                        inboundId);
                    await DeleteClientFromInboundAsync(client.Id, cancellationToken).ConfigureAwait(false);
                    removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка 3X-UI при очистке просроченных клиентов.");
            return removed;
        }
    }

    // ===== Создание клиента =====

    /// <summary>
    /// Создаёт клиента через <c>POST /panel/api/clients/add</c>.
    /// Тело: <c>{"client": &lt;object&gt;, "inboundIds": [&lt;inboundId&gt;]}</c>.
    /// </summary>
    private async Task AddClientToInboundAsync(
        long inboundId,
        string clientUuid,
        string email,
        long expiryMs,
        CancellationToken cancellationToken)
    {
        var client = BuildClientObject(clientUuid, email, expiryMs, null, null, null);

        var payload = new JsonObject
        {
            ["client"] = client,
            ["inboundIds"] = new JsonArray(inboundId)
        }.ToJsonString();

        _logger.LogInformation(
            "3X-UI: добавляю клиента {Uuid} (inbound {InboundId}). Payload: {Payload}",
            clientUuid,
            inboundId,
            payload);

        var url = $"{ApiBase_Api}/clients/add";
        var body = await PostJsonAndReadAsync(url, payload, cancellationToken).ConfigureAwait(false);
        var ok = await EnsureSuccessAsync(body, "добавление клиента", clientUuid, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            _logger.LogInformation("3X-UI: клиент {Uuid} успешно создан в инбаунде {InboundId}.", clientUuid, inboundId);
        }
    }

    /// <summary>
    /// Обновляет существующего клиента через <c>POST /panel/api/clients/update/{uuid}</c>.
    /// Тело — объект клиента напрямую (без обёртки <c>client</c>/<c>settings</c>).
    /// </summary>
    private async Task UpdateClientInInboundAsync(
        string clientUuid,
        string email,
        long expiryMs,
        string? subId,
        string? password,
        string? auth,
        CancellationToken cancellationToken)
    {
        var client = BuildClientObject(clientUuid, email, expiryMs, subId, password, auth);
        var payload = client.ToJsonString();

        _logger.LogInformation(
            "3X-UI: продлеваю клиента {Uuid}. Payload: {Payload}",
            clientUuid,
            payload);

        var url = $"{ApiBase_Api}/clients/update/{Uri.EscapeDataString(clientUuid)}";
        var body = await PostJsonAndReadAsync(url, payload, cancellationToken).ConfigureAwait(false);
        var ok = await EnsureSuccessAsync(body, "продление клиента", clientUuid, cancellationToken).ConfigureAwait(false);
        if (ok)
        {
            _logger.LogInformation("3X-UI: клиент {Uuid} продлён до {Expiry}.", clientUuid, expiryMs);
        }
    }

    /// <summary>
    /// Удаляет клиента: <c>POST /panel/api/clients/del/{uuid}</c> (тело пустое).
    /// </summary>
    private async Task DeleteClientFromInboundAsync(
        string clientUuid,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiBase_Api}/clients/del/{Uri.EscapeDataString(clientUuid)}";
        _logger.LogInformation("3X-UI: удаляю клиента {Uuid}.", clientUuid);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        Authorize(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("3X-UI: удаление клиента {Uuid} -> HTTP {Status}: {Response}",
            clientUuid, (int)response.StatusCode, body);

        await EnsureSuccessAsync(body, "удаление клиента", clientUuid, cancellationToken).ConfigureAwait(false);
    }

    // ===== Чтение инбаундов и клиентов =====

    /// <summary>
    /// Определяет ID инбаунда: из конфигурации <c>PanelInboundId</c> (по умолчанию 2)
    /// либо первый VLESS-инбаунд.
    /// </summary>
    private async Task<long?> ResolveInboundIdAsync(CancellationToken cancellationToken)
    {
        if (_vpnConfig.InboundId > 0)
        {
            return _vpnConfig.InboundId;
        }

        var ids = await ListInboundIdsAsync(cancellationToken).ConfigureAwait(false);
        return ids.FirstOrDefault();
    }

    private async Task<IReadOnlyList<long>> ListInboundIdsAsync(CancellationToken cancellationToken)
    {
        var url = $"{ApiInboundsBase}/list";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("3X-UI: список инбаундов -> HTTP {Status}: {Response}",
            (int)response.StatusCode, body);

        var doc = JsonNode.Parse(body);
        var arr = doc?["obj"]?.AsArray();
        if (arr == null)
        {
            return Array.Empty<long>();
        }

        return arr
            .Where(n => n != null && string.Equals(n["protocol"]?.GetValue<string>() ?? "", "vless", StringComparison.OrdinalIgnoreCase))
            .Select(n => n!["id"]?.GetValue<long>() ?? 0)
            .Where(id => id > 0)
            .ToList();
    }

    /// <summary>
    /// Возвращает клиентов конкретного инбаунда, читая <c>obj.settings.clients</c>
    /// через <c>GET /panel/api/inbounds/get/{id}</c>.
    /// </summary>
    private async Task<IReadOnlyList<XuiClientEntry>> LoadInboundClientsAsync(
        long inboundId,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiInboundsBase}/get/{inboundId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var doc = JsonNode.Parse(body);
        var clients = doc?["obj"]?["settings"]?["clients"]?.AsArray();
        if (clients == null)
        {
            return Array.Empty<XuiClientEntry>();
        }

        var result = new List<XuiClientEntry>();
        foreach (var node in clients)
        {
            if (node == null)
            {
                continue;
            }
            result.Add(new XuiClientEntry
            {
                Id = node["id"]?.GetValue<string>() ?? string.Empty,
                Email = node["email"]?.GetValue<string>(),
                SubId = node["subId"]?.GetValue<string>(),
                Password = node["password"]?.GetValue<string>(),
                Auth = node["auth"]?.GetValue<string>(),
                ExpiryTime = node["expiryTime"]?.GetValue<long>() ?? 0
            });
        }
        return result;
    }

    private async Task<XuiClientEntry?> FindClientByUuidAsync(
        string clientUuid,
        CancellationToken cancellationToken)
    {
        var ids = await ListInboundIdsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var inboundId in ids)
        {
            var clients = await LoadInboundClientsAsync(inboundId, cancellationToken).ConfigureAwait(false);
            var match = clients.FirstOrDefault(
                c => string.Equals(c.Id, clientUuid, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return new XuiClientEntry
                {
                    Id = match.Id,
                    Email = match.Email,
                    SubId = match.SubId,
                    Password = match.Password,
                    Auth = match.Auth,
                    ExpiryTime = match.ExpiryTime,
                    InboundId = inboundId
                };
            }
        }
        return null;
    }

    private async Task<XuiClientEntry?> FindClientAsync(
        string clientUuid,
        string email,
        CancellationToken cancellationToken)
    {
        var ids = await ListInboundIdsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var inboundId in ids)
        {
            var clients = await LoadInboundClientsAsync(inboundId, cancellationToken).ConfigureAwait(false);
            var match = clients.FirstOrDefault(
                c => string.Equals(c.Id, clientUuid, StringComparison.OrdinalIgnoreCase)
                     || (!string.IsNullOrWhiteSpace(c.Email)
                         && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));
            if (match != null)
            {
                return new XuiClientEntry
                {
                    Id = match.Id,
                    Email = match.Email,
                    SubId = match.SubId,
                    Password = match.Password,
                    Auth = match.Auth,
                    ExpiryTime = match.ExpiryTime,
                    InboundId = inboundId
                };
            }
        }
        return null;
    }

    // ===== Построение payload =====

    private static JsonObject BuildClientObject(
        string clientUuid,
        string email,
        long expiryMs,
        string? subId,
        string? password,
        string? auth)
    {
        return new JsonObject
        {
            ["email"] = email,
            ["subId"] = string.IsNullOrEmpty(subId) ? GenerateSubId() : subId,
            ["id"] = clientUuid,
            ["password"] = string.IsNullOrEmpty(password) ? GenerateSubId() : password,
            ["auth"] = string.IsNullOrEmpty(auth) ? GenerateSubId() : auth,
            ["flow"] = "",          // gRPC Reality: Flow строго пустой.
            ["security"] = "auto",
            ["totalGB"] = 0,
            ["expiryTime"] = expiryMs,
            ["reset"] = 0,
            ["limitIp"] = 0,
            ["tgId"] = 0,
            ["group"] = "",
            ["comment"] = "",
            ["enable"] = true
        };
    }

    /// <summary>
    /// Генерирует случайную hex-строку для <c>subId</c> (переиспользуется при подписке).
    /// </summary>
    private static string GenerateSubId()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    // ===== Отправка и проверка ответа =====

    private async Task<string> PostJsonAndReadAsync(
        string url,
        string payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("3X-UI POST {Url} -> HTTP {Status}: {Response}", url, (int)response.StatusCode, body);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"3X-UI вернул HTTP {(int)response.StatusCode} для {url}: {body}");
        }

        return body;
    }

    /// <summary>
    /// Проверяет JSON-ответ панели. Успех — только при <c>success == true</c>.
    /// При <c>success == false</c> логирует <c>msg</c> из ответа и выбрасывает
    /// исключение (блокирует выдачу нерабочей ссылки).
    /// </summary>
    private Task<bool> EnsureSuccessAsync(
        string body,
        string operation,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            var error = $"3X-UI: пустой ответ при {operation} клиента {clientId}.";
            _logger.LogError("{Error}", error);
            throw new InvalidOperationException(error);
        }

        JsonNode? doc = null;
        try
        {
            doc = JsonNode.Parse(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "3X-UI: не удалось разобрать ответ при {Operation} клиента {ClientId}: {Body}",
                operation, clientId, body);
        }

        var success = doc?["success"]?.GetValue<bool>() ?? false;
        if (success)
        {
            return Task.FromResult(true);
        }

        var msg = doc?["msg"]?.GetValue<string>() ?? "unknown";
        var errorText = $"3X-UI: операция «{operation}» клиента {clientId} отклонена панелью (success=false). Ответ: {body}";
        _logger.LogError("{Error}; msg={Msg}", errorText, msg);
        throw new InvalidOperationException(errorText);
    }

    private static long ToUnixTimeMs(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}

/// <summary>Модель клиента 3X-UI, извлекаемая из <c>settings.clients</c> инбаунда.</summary>
internal sealed class XuiClientEntry
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? SubId { get; set; }
    public string? Password { get; set; }
    public string? Auth { get; set; }
    public long ExpiryTime { get; set; }
    public long InboundId { get; set; }
}
