using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class ForumRepository : IForumRepository
{
    private readonly DbBronyTV _context;

    public ForumRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ForumThreadEntity>> GetThreadsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.ForumThreads
            .AsNoTracking()
            .Include(thread => thread.Author)
            .Include(thread => thread.Posts)
            .OrderByDescending(thread => thread.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<ForumThreadEntity?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default) =>
        _context.ForumThreads
            .AsNoTracking()
            .Include(thread => thread.Author)
            .FirstOrDefaultAsync(thread => thread.Id == threadId, cancellationToken);

    public async Task<ForumThreadEntity> AddThreadAsync(
        ForumThreadEntity thread,
        CancellationToken cancellationToken = default)
    {
        _context.ForumThreads.Add(thread);
        await _context.SaveChangesAsync(cancellationToken);
        return thread;
    }

    public async Task<IReadOnlyList<ForumPostEntity>> GetPostsByThreadIdAsync(
        Guid threadId,
        CancellationToken cancellationToken = default) =>
        await _context.ForumPosts
            .AsNoTracking()
            .Include(post => post.Author)
            .Where(post => post.ThreadId == threadId)
            .OrderBy(post => post.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<ForumPostEntity> AddPostAsync(ForumPostEntity post, CancellationToken cancellationToken = default)
    {
        _context.ForumPosts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);
        return post;
    }
}
