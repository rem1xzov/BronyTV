using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BronyTV.Service;

/// <summary>
/// Начисляет дни премиум-доступа к ИИ-боту. Премиум хранится в AiBronyTV
/// (таблица ai."UserLimits"."PremiumUntil"), поэтому начисление идёт server-to-server
/// вызовом в AiBronyTV под общим внутренним ключом (BRONYTV_INTERNAL_KEY).
/// </summary>
public interface IPremiumRewardClient
{
    Task GrantPremiumDaysAsync(Guid userId, int days, CancellationToken cancellationToken = default);
}

public class PremiumRewardClient : IPremiumRewardClient
{
    private readonly HttpClient _httpClient;
    private readonly string _internalKey;
    private readonly ILogger<PremiumRewardClient> _logger;

    public PremiumRewardClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PremiumRewardClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AiBackend");
        _internalKey = configuration["InternalApiKey"]
            ?? Environment.GetEnvironmentVariable("BRONYTV_INTERNAL_KEY")
            ?? string.Empty;
        _logger = logger;
    }

    public async Task GrantPremiumDaysAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { userId = userId.ToString(), days });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/premium/grant");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Internal-Key", _internalKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "AiBronyTV: не удалось начислить {Days} дней премиум пользователю {UserId}: {Status} {Body}",
                days,
                userId,
                (int)response.StatusCode,
                body);
        }
    }
}
