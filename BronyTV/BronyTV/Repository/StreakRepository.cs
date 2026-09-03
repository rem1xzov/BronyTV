using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class StreakRepository : IStreakRepository
{
    private readonly DbBronyTV _context;

    public StreakRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<UserStreakEntity?> GetStreakAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId, cancellationToken);
    }

    public async Task<UserStreakEntity> GetOrCreateStreakAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var streak = await _context.UserStreaks
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (streak == null)
        {
            streak = new UserStreakEntity
            {
                UserId = userId,
                CurrentStreak = 0,
                LongestStreak = 0,
                LastActiveDate = default,
                FreezesAvailable = 3,
                FreezesUsedThisMonth = 0,
                FreezesMonth = 0
            };
            _context.UserStreaks.Add(streak);
        }

        return streak;
    }

    public async Task<DailyActivityProgressEntity?> GetProgressAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyActivityProgress
            .FirstOrDefaultAsync(
                progress => progress.UserId == userId && progress.Date == date,
                cancellationToken);
    }

    public async Task<DailyActivityProgressEntity> GetOrCreateProgressAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var progress = await _context.DailyActivityProgress
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.Date == date,
                cancellationToken);

        if (progress == null)
        {
            progress = new DailyActivityProgressEntity
            {
                UserId = userId,
                Date = date,
                ActiveMinutes = 0m,
                QualifyingCommentsCount = 0,
                IsStreakCredited = false
            };
            _context.DailyActivityProgress.Add(progress);
        }

        return progress;
    }

    public async Task<StreakRewardsClaimedEntity?> GetRewardAsync(
        Guid userId,
        int milestone,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreakRewardsClaimed
            .FirstOrDefaultAsync(
                reward => reward.UserId == userId && reward.Milestone == milestone,
                cancellationToken);
    }

    public async Task<IReadOnlyList<StreakRewardsClaimedEntity>> GetRewardsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreakRewardsClaimed
            .AsNoTracking()
            .Where(reward => reward.UserId == userId)
            .OrderBy(reward => reward.Milestone)
            .ToListAsync(cancellationToken);
    }

    public void AddReward(StreakRewardsClaimedEntity reward)
    {
        _context.StreakRewardsClaimed.Add(reward);
    }

    public void AddPendingManualReward(PendingManualRewardEntity reward)
    {
        _context.PendingManualRewards.Add(reward);
    }

    public async Task<IReadOnlyList<StreakLeaderboardRow>> GetLeaderboardAsync(
        bool byLongest,
        int limit,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        var query = _context.UserStreaks
            .AsNoTracking()
            .Where(streak => streak.CurrentStreak > 0 || streak.LongestStreak > 0);

        query = byLongest
            ? query.OrderByDescending(streak => streak.LongestStreak).ThenByDescending(streak => streak.CurrentStreak)
            : query.OrderByDescending(streak => streak.CurrentStreak).ThenByDescending(streak => streak.LongestStreak);

        return await query
            .Take(safeLimit)
            .Select(streak => new StreakLeaderboardRow
            {
                UserId = streak.UserId,
                Username = streak.User!.Username ?? streak.User!.Email,
                CurrentStreak = streak.CurrentStreak,
                LongestStreak = streak.LongestStreak,
                IsCreditedToday = _context.DailyActivityProgress.Any(
                    progress => progress.UserId == streak.UserId
                        && progress.Date == today
                        && progress.IsStreakCredited)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, StreakSummary>> GetStreakSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, StreakSummary>();
        }

        var distinctIds = userIds.Distinct().ToList();

        var streaks = await _context.UserStreaks
            .AsNoTracking()
            .Where(streak => distinctIds.Contains(streak.UserId))
            .Select(streak => new { streak.UserId, streak.CurrentStreak })
            .ToListAsync(cancellationToken);

        var creditedIds = await _context.DailyActivityProgress
            .AsNoTracking()
            .Where(progress => distinctIds.Contains(progress.UserId)
                && progress.Date == today
                && progress.IsStreakCredited)
            .Select(progress => progress.UserId)
            .ToListAsync(cancellationToken);

        var creditedSet = creditedIds.ToHashSet();
        var result = new Dictionary<Guid, StreakSummary>();
        foreach (var streak in streaks)
        {
            result[streak.UserId] = new StreakSummary(streak.CurrentStreak, creditedSet.Contains(streak.UserId));
        }

        return result;
    }

    public async Task<int> MarkRewardsSeenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreakRewardsClaimed
            .Where(reward => reward.UserId == userId && !reward.IsRewardSeen)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(reward => reward.IsRewardSeen, true),
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
