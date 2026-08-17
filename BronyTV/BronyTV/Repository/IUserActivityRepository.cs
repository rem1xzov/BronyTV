using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivityEntity activity, CancellationToken cancellationToken = default);
    Task<bool> HasRecentAsync(
        Guid userId,
        string activityType,
        string? details,
        TimeSpan within,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserActivityEntity>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
