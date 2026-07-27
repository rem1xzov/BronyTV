using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IForumRepository
{
    Task<IReadOnlyList<ForumThreadEntity>> GetThreadsAsync(CancellationToken cancellationToken = default);
    Task<ForumThreadEntity?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<ForumThreadEntity> AddThreadAsync(ForumThreadEntity thread, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumPostEntity>> GetPostsByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<ForumPostEntity?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<ForumPostEntity> AddPostAsync(ForumPostEntity post, CancellationToken cancellationToken = default);
    Task UpdatePostAsync(ForumPostEntity post, CancellationToken cancellationToken = default);
    Task DeletePostAsync(ForumPostEntity post, CancellationToken cancellationToken = default);
}
