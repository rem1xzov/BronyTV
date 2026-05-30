using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IUserRepository
{
    Task<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsForOtherUserAsync(string username, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default);
    Task<UserEntity> SaveChangesAsync(UserEntity user, CancellationToken cancellationToken = default);
}
