using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IUserRepository
{
    Task<UserEntity?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default);
    Task<bool> TrySetRaceAsync(Guid userId, string race, CancellationToken cancellationToken = default);
}
