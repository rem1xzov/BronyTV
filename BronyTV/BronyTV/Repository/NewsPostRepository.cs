using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using BronyTV.Repository;

namespace BronyTV.Repository;

public class NewsPostRepository : INewsPostRepository
{
    private readonly DbBronyTV _context;

    public NewsPostRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NewsPost>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.NewsPosts
            .AsNoTracking()
            .OrderByDescending(news => news.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<NewsPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.NewsPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(news => news.Id == id, cancellationToken);

    public async Task<NewsPost> AddAsync(NewsPost news, CancellationToken cancellationToken = default)
    {
        _context.NewsPosts.Add(news);
        await _context.SaveChangesAsync(cancellationToken);
        return news;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var news = await _context.NewsPosts.FindAsync(new object[] { id }, cancellationToken);
        if (news != null)
        {
            _context.NewsPosts.Remove(news);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
