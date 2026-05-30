using BronyTV.Contract;
using BronyTV.DbContext.Entity;

namespace BronyTV.Service;

public interface IUserAuthService
{
    Task<(AuthUserResponse? Response, string? Error)> RegisterAsync(
        string email,
        string password,
        string race,
        CancellationToken cancellationToken = default);

    Task<UserEntity?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    string CreateSessionToken(UserEntity user);
    AuthUserResponse MapUserResponse(UserEntity user);

    Task<(AuthUserResponse? Response, string? Error)> UpdateUsernameAsync(
        Guid userId,
        string username,
        CancellationToken cancellationToken = default);
}
