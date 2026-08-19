using System;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BronyTV.Service;

/// <summary>
/// Фоновая задача: раз в час принудительно удаляет с панели 3X-UI
/// клиентов, срок действия которых истёк. Это дополняет check-логику,
/// которая на фронтенде учитывает дату окончания подписки.
/// </summary>
public class VpnExpiryCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VpnExpiryCleanupHostedService> _logger;
    private readonly TimeSpan _interval;

    public VpnExpiryCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<VpnExpiryCleanupHostedService> logger,
        IOptions<VpnOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromHours(1);
        var _ = options; // настройка читается на каждом цикле
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IVpn3xUiClient>();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<VpnOptions>>().Value;
                if (options.Enabled)
                {
                    var removed = await client.DisableExpiredAsync(stoppingToken).ConfigureAwait(false);
                    if (removed > 0)
                    {
                        _logger.LogInformation("3X-UI: отключено {Count} просроченных клиентов.", removed);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка фоновой очистки просроченных VPN-клиентов.");
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
