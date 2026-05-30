using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface ICommentRepository
{
    Task<IReadOnlyList<CommentEntity>> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<CommentEntity?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> VideoExistsAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<CommentEntity> AddAsync(CommentEntity comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(CommentEntity comment, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> GetLikeCountsByCommentIdsAsync(
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetLikedCommentIdsForUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default);
    Task<CommentLikeEntity?> GetLikeAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);
    Task AddLikeAsync(CommentLikeEntity like, CancellationToken cancellationToken = default);
    Task RemoveLikeAsync(CommentLikeEntity like, CancellationToken cancellationToken = default);
    Task<int> GetLikeCountAsync(Guid commentId, CancellationToken cancellationToken = default);
}
