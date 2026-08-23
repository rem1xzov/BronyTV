namespace BronyTV.DbContext.Entity;

public class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? AvatarEmoji { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime RaceSelectedAtUtc { get; set; }
    public bool IsBannedFromCommenting { get; set; }
    public string PlatformRole { get; set; } = "User";
    public bool IsEmailConfirmed { get; set; }
    // Stores a BCrypt hash of the one-time email code, never the code itself.
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationExpiresAtUtc { get; set; }
        public DateTime? EmailConfirmationLastSentAtUtc { get; set; }
    public int EmailConfirmationFailedAttempts { get; set; }

    // Сброс пароля — отдельный контекст со СВОИМ набором полей, чтобы код сброса
    // никогда не совпадал с кодом подтверждения регистрации (и наоборот). Историю и
    // лимиты кода переиспользуем из подтверждения email (null-поля и 5 попыток).
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresAtUtc { get; set; }
    public DateTime? PasswordResetLastSentAtUtc { get; set; }
    public int PasswordResetFailedAttempts { get; set; }

    // Реферальная система BronyVPN: уникальный код для приглашения друзей
    // и ссылка на пользователя, по чьей ссылке зарегистрировался инвайт.
    public string? ReferralCode { get; set; }
    public Guid? ReferredByUserId { get; set; }
}
