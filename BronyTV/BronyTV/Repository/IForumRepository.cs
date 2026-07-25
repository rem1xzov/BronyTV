using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IForumRepository
{
    Task<IReadOnlyList<ForumThreadEntity>> GetThreadsAsync(CancellationToken cancellationToken = default);
    Task<ForumThreadEntity?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<ForumThreadEntity> AddThreadAsync(ForumThreadEntity thread, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumPostEntity>> GetPostsByThreadIdAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);
    Task<ForumPostEntity> AddPostAsync(ForumPostEntity post, CancellationToken cancellationToken = default);
}
