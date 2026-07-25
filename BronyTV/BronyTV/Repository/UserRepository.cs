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

    public Task<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> UsernameExistsForOtherUserAsync(
        string username,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            user => user.Username != null
                    && user.Username == username
                    && user.Id != userId,
            cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Username == username, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public async Task<IReadOnlyList<UserEntity>> SearchByUsernameOrEmailAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return Array.Empty<UserEntity>();
        }

        var emailQuery = $"%{normalized}%";
        return await _context.Users
            .AsNoTracking()
            .Where(user =>
                (user.Username != null && EF.Functions.ILike(user.Username, emailQuery))
                || EF.Functions.ILike(user.Email, emailQuery))
            .OrderBy(user => user.Username ?? user.Email)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<UserEntity> Items, int TotalCount)> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safePageSize;

        var query = _context.Users.AsNoTracking().OrderByDescending(user => user.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(safePageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<UserEntity> SaveChangesAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task DeleteAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
