using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface ICommentRepository
{
    Task<IReadOnlyList<CommentEntity>> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<CommentEntity?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> VideoExistsAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<CommentEntity> AddAsync(CommentEntity comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(CommentEntity comment, CancellationToken cancellationToken = default);
}
