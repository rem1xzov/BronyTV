using BronyTV.Contract;
using BronyTV.DbContext.Entity;

namespace BronyTV.Service;

public interface IUserAuthService
{
    Task<(AuthUserResponse? Response, string? Error)> RegisterAsync(
        string email,
        string password,
        string race,
        string username,
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

    Task<(bool Success, string? Error)> UpdatePasswordAsync(
        Guid userId,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);

    Task<(AuthUserResponse? Response, string? Error)> UpdateAvatarEmojiAsync(
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default);
}
