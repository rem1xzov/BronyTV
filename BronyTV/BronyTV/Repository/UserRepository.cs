using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class UserRepository : IUserRepository
{
    private readonly DbBronyTV _context;

    public UserRepository(DbBronyTV context)
    {
        _context = context;
    }

    public Task<UserEntity?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default) =>
        _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.GoogleSub == googleSub, cancellationToken);

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> TrySetRaceAsync(Guid userId, string race, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        if (user == null || !string.IsNullOrEmpty(user.Race))
        {
            return false;
        }

        user.Race = race;
        user.RaceSelectedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
