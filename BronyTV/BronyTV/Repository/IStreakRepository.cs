using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

/// <summary>Строка таблицы лидеров, собираемая одним запросом в репозитории.</summary>
public sealed class StreakLeaderboardRow
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public bool IsCreditedToday { get; set; }
}

/// <summary>Краткая сводка стрика для отображения огонька (без полной сущности).</summary>
public sealed record StreakSummary(int CurrentStreak, bool IsCreditedToday);

public interface IStreakRepository
{
    Task<UserStreakEntity?> GetStreakAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserStreakEntity> GetOrCreateStreakAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DailyActivityProgressEntity?> GetProgressAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<DailyActivityProgressEntity> GetOrCreateProgressAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<StreakRewardsClaimedEntity?> GetRewardAsync(
        Guid userId,
        int milestone,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreakRewardsClaimedEntity>> GetRewardsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    void AddReward(StreakRewardsClaimedEntity reward);
    void AddPendingManualReward(PendingManualRewardEntity reward);

    Task<IReadOnlyList<StreakLeaderboardRow>> GetLeaderboardAsync(
        bool byLongest,
        int limit,
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>Батч-загрузка текущих стриков для набора пользователей (для огоньков).</summary>
    Task<IReadOnlyDictionary<Guid, StreakSummary>> GetStreakSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>Помечает все непоказанные награды пользователя как показанные.</summary>
    Task<int> MarkRewardsSeenAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
