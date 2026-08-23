using BronyTV.Contract;
using BronyTV.DbContext.Entity;

namespace BronyTV.Service;

public interface IUserAuthService
{
    Task<(RegistrationPendingResponse? Response, string? Error)> RegisterAsync(
    string email,
    string password,
    string race,
    string username,
    string? referralCode = null,
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

    Task<(bool Success, string? Error)> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);

        Task<(bool Success, string? Error)> ResendEmailConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Запрашивает 6-значный код для сброса пароля. Возвращает false с явным сообщением,
    /// если аккаунт с таким email не найден. Код привязан к отдельному "контексту сброса"
    /// и не совпадает с кодом подтверждения регистрации.
    /// </summary>
    Task<(bool Success, string? Error)> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет код сброса пароля (отдельная история попыток, как у регистрации) и меняет
    /// пароль на новый. Возвращает false с сообщением об ошибке при неверном/просроченном
    /// коде или невалидном пароле.
    /// </summary>
    Task<(bool Success, string? Error)> ConfirmPasswordResetAsync(
        string email,
        string code,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);
}
