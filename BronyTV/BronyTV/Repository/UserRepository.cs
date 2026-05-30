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

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public async Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }
}
