using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class NewsCommentRepository : INewsCommentRepository
{
    private readonly DbBronyTV _context;

    public NewsCommentRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NewsCommentEntity>> GetByNewsIdAsync(
        Guid newsId,
        CancellationToken cancellationToken = default) =>
        await _context.NewsComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.ReplyToComment)
                .ThenInclude(reply => reply!.Author)
            .Where(comment => comment.NewsId == newsId)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<NewsCommentEntity?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _context.NewsComments
            .Include(comment => comment.Author)
            .Include(comment => comment.ReplyToComment)
                .ThenInclude(reply => reply!.Author)
            .FirstOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task<NewsCommentEntity> AddAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default)
    {
        _context.NewsComments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task UpdateAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default)
    {
        _context.NewsComments.Update(comment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(NewsCommentEntity comment, CancellationToken cancellationToken = default)
    {
        _context.NewsComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
