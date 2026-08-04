using BronyTV.DbContext;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Infrastructure;

/// <summary>
/// Periodically removes stale unconfirmed user records from the database.
/// With the pending-cache registration flow no unconfirmed rows are normally created,
/// but legacy rows and any edge cases are cleaned up after 24 hours so they never
/// accumulate and never appear in the admin panel.
/// </summary>
public class ConfirmationCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfirmationCleanupHostedService> _logger;
    private readonly TimeSpan _cutoffAge = TimeSpan.FromHours(24);

    public ConfirmationCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConfirmationCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run an initial cleanup shortly after startup, then every hour.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ConfirmationCleanupHostedService: ошибка при очистке неподтверждённых пользователей.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - _cutoffAge;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DbBronyTV>();

        var stale = await context.Users
            .Where(u => !u.IsEmailConfirmed && u.CreatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        context.Users.RemoveRange(stale);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ConfirmationCleanupHostedService: удалено неподтверждённых пользователей старше {Hours} ч: {Count}",
            _cutoffAge.TotalHours,
            stale.Count);
    }
}
