using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;

namespace BronyTV.Service;

public interface IUserFavoriteService
{
    Task<bool> IsFavoriteAsync(Guid userId, Guid videoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteItemResponse>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Guid userId, Guid videoId, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid userId, Guid videoId, CancellationToken cancellationToken = default);
}
