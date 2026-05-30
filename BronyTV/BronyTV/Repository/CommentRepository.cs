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

    public async Task<Dictionary<Guid, int>> GetLikeCountsByCommentIdsAsync(
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default)
    {
        if (commentIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _context.CommentLikes
            .AsNoTracking()
            .Where(like => commentIds.Contains(like.CommentId))
            .GroupBy(like => like.CommentId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
    }

    public async Task<HashSet<Guid>> GetLikedCommentIdsForUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default)
    {
        if (commentIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var likedIds = await _context.CommentLikes
            .AsNoTracking()
            .Where(like => like.UserId == userId && commentIds.Contains(like.CommentId))
            .Select(like => like.CommentId)
            .ToListAsync(cancellationToken);

        return likedIds.ToHashSet();
    }

    public Task<CommentLikeEntity?> GetLikeAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default) =>
        _context.CommentLikes.FirstOrDefaultAsync(
            like => like.UserId == userId && like.CommentId == commentId,
            cancellationToken);

    public async Task AddLikeAsync(CommentLikeEntity like, CancellationToken cancellationToken = default)
    {
        _context.CommentLikes.Add(like);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLikeAsync(CommentLikeEntity like, CancellationToken cancellationToken = default)
    {
        _context.CommentLikes.Remove(like);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetLikeCountAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _context.CommentLikes.CountAsync(like => like.CommentId == commentId, cancellationToken);
}
