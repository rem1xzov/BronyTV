using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface INewsCommentRepository
{
    Task<IReadOnlyList<NewsCommentEntity>> GetByNewsIdAsync(Guid newsId, CancellationToken cancellationToken = default);
    Task<NewsCommentEntity?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task<NewsCommentEntity> AddAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default);
    Task UpdateAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default);
}
