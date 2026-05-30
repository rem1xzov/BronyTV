using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class CommentRepository : ICommentRepository
{
    private readonly DbBronyTV _context;

    public CommentRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CommentEntity>> GetByVideoIdAsync(
        Guid videoId,
        CancellationToken cancellationToken = default) =>
        await _context.Comments
            .AsNoTracking()
            .Include(comment => comment.User)
            .Where(comment => comment.VideoId == videoId)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<CommentEntity?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _context.Comments.FirstOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public Task<bool> VideoExistsAsync(Guid videoId, CancellationToken cancellationToken = default) =>
        _context.Videos.AnyAsync(video => video.Id == videoId, cancellationToken);

    public async Task<CommentEntity> AddAsync(CommentEntity comment, CancellationToken cancellationToken = default)
    {
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task DeleteAsync(CommentEntity comment, CancellationToken cancellationToken = default)
    {
        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
