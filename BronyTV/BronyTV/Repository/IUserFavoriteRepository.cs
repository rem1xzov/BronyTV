using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IUserFavoriteRepository
{
    Task<bool> IsFavoriteAsync(Guid userId, Guid videoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserFavoriteEntity>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(UserFavoriteEntity favorite, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid userId, Guid videoId, CancellationToken cancellationToken = default);
}
