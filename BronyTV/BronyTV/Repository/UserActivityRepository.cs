using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class UserActivityRepository : IUserActivityRepository
{
    private readonly DbBronyTV _context;

    public UserActivityRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task AddAsync(
        UserActivityEntity activity,
        CancellationToken cancellationToken = default)
    {
        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasRecentAsync(
        Guid userId,
        string activityType,
        string? details,
        TimeSpan within,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - within;

        // Normalise null so that "no details" and "empty details" are treated equally.
        var detailsKey = string.IsNullOrWhiteSpace(details) ? null : details.Trim();

        return await _context.UserActivities
            .AsNoTracking()
            .AnyAsync(
                activity => activity.UserId == userId
                    && activity.ActivityType == activityType
                    && activity.Details == detailsKey
                    && activity.Timestamp >= since,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserActivityEntity>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        return await _context.UserActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId)
            .OrderByDescending(activity => activity.Timestamp)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserActivityEntity>> GetRecentAllUsersAsync(
        int days = 7,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var safeDays = Math.Clamp(days, 1, 365);
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var since = DateTime.UtcNow.AddDays(-safeDays);

        return await _context.UserActivities
            .AsNoTracking()
            .Where(activity => activity.Timestamp >= since && !activity.HiddenFromAdmin)
            .OrderByDescending(activity => activity.Timestamp)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HideFromAdminAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await _context.UserActivities
            .Where(activity => activity.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(activity => activity.HiddenFromAdmin, true),
                cancellationToken);

        return updated > 0;
    }

    public async Task<int> DeleteOlderThanAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - maxAge;

        var deleted = await _context.UserActivities
            .Where(activity => activity.Timestamp < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }
}
