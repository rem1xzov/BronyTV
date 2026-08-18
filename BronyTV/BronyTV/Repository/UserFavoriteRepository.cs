using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class UserFavoriteRepository : IUserFavoriteRepository
{
    private readonly DbBronyTV _context;

    public UserFavoriteRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<bool> IsFavoriteAsync(
        Guid userId,
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserFavorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.VideoId == videoId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserFavoriteEntity>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserFavorites
            .AsNoTracking()
            .Include(favorite => favorite.Video)
                .ThenInclude(video => video!.Season)
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        UserFavoriteEntity favorite,
        CancellationToken cancellationToken = default)
    {
        _context.UserFavorites.Add(favorite);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await _context.UserFavorites
            .FirstOrDefaultAsync(
                favorite => favorite.UserId == userId && favorite.VideoId == videoId,
                cancellationToken);

        if (favorite == null)
        {
            return false;
        }

        _context.UserFavorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
