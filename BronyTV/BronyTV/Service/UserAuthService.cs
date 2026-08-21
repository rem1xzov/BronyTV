using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.IdentityModel.Tokens;

namespace BronyTV.Service;

public class UserAuthService : IUserAuthService
{
    private const int ConfirmationLifetimeMinutes = 15;
    private const int ConfirmationResendCooldownSeconds = 60;
    private const int MaxConfirmationAttempts = 5;

    private readonly IUserRepository _userRepository;
    private readonly IAdminAccessService _adminAccessService;
    private readonly IEmailService _emailService;
    private readonly IVpnRepository _vpnRepository;
    private readonly IConfiguration _configuration;

    public UserAuthService(
        IUserRepository userRepository,
        IAdminAccessService adminAccessService,
        IEmailService emailService,
        IVpnRepository vpnRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _adminAccessService = adminAccessService;
        _emailService = emailService;
        _vpnRepository = vpnRepository;
        _configuration = configuration;
    }

    public async Task<(RegistrationPendingResponse? Response, string? Error)> RegisterAsync(
    string email,
    string password,
    string race,
    string username,
    string? referralCode = null,
    CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return (null, "Укажите корректный email.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
        {
            return (null, "Пароль должен содержать от 8 до 128 символов.");
        }

        if (!UserRace.TryNormalize(race, out var normalizedRace))
        {
            return (null, "Выберите расу: пегасы, единороги или земные пони.");
        }

        if (!UsernameRules.TryNormalize(username, out var normalizedUsername, out var usernameError))
        {
            return (null, usernameError);
        }

        // Реферальная система BronyVPN: резолвим пригласившего по коду, если он указан.
        // Игнорируем неверный/несуществующий код (мягкая деградация), но НЕ даём
        // пригласить самого себя (это выяснится после создания аккаунта).
        Guid? referredByUserId = null;
        if (!string.IsNullOrWhiteSpace(referralCode))
        {
            var referrer = await _userRepository.GetByReferralCodeAsync(
                referralCode.Trim(),
                cancellationToken);
            if (referrer != null)
            {
                referredByUserId = referrer.Id;
            }
        }

        var existing = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing?.IsEmailConfirmed == true)
        {
            return (null, "Пользователь с таким email уже зарегистрирован.");
        }

        var pendingUserId = existing?.Id ?? Guid.Empty;
        if (await _userRepository.UsernameExistsForOtherUserAsync(
                normalizedUsername,
                pendingUserId,
                cancellationToken))
        {
            return (null, "Этот юзернейм уже занят.");
        }

        var now = DateTime.UtcNow;
        var confirmationCode = CreateEmailConfirmationCode();
        var confirmationHash = BCrypt.Net.BCrypt.HashPassword(confirmationCode);

        // Уникальный реферальный код — генерируем до тех пор, пока не найдём свободный.
        var newReferralCode = await GenerateUniqueReferralCodeAsync(cancellationToken);

        if (existing == null)
        {
            existing = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Username = normalizedUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Race = normalizedRace,
                CreatedAtUtc = now,
                RaceSelectedAtUtc = now,
                IsBannedFromCommenting = false,
                PlatformRole = _adminAccessService.ResolveInitialRole(normalizedEmail),
                IsEmailConfirmed = false,
                EmailConfirmationToken = confirmationHash,
                EmailConfirmationExpiresAtUtc = now.AddMinutes(ConfirmationLifetimeMinutes),
                EmailConfirmationLastSentAtUtc = now,
                EmailConfirmationFailedAttempts = 0,
                ReferralCode = newReferralCode,
                ReferredByUserId = referredByUserId
            };

            await _userRepository.CreateAsync(existing, cancellationToken);
        }
        else
        {
            // A repeated registration for an unconfirmed address replaces the pending data
            // and issues a new one-time code. This also makes recovery after a restart safe.
            existing.Username = normalizedUsername;
            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            existing.Race = normalizedRace;
            existing.CreatedAtUtc = now;
            existing.RaceSelectedAtUtc = now;
            existing.PlatformRole = _adminAccessService.ResolveInitialRole(normalizedEmail);
            existing.ReferralCode ??= newReferralCode;
            existing.ReferredByUserId ??= referredByUserId;
            existing.EmailConfirmationToken = confirmationHash;
            existing.EmailConfirmationExpiresAtUtc = now.AddMinutes(ConfirmationLifetimeMinutes);
            existing.EmailConfirmationLastSentAtUtc = now;
            existing.EmailConfirmationFailedAttempts = 0;
            await _userRepository.SaveChangesAsync(existing, cancellationToken);
        }

        try
        {
            await _emailService.SendEmailConfirmationAsync(
                normalizedEmail,
                confirmationCode,
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Do not enforce a cooldown for a message that the SMTP server did not accept.
            existing.EmailConfirmationLastSentAtUtc = null;
            await _userRepository.SaveChangesAsync(existing, CancellationToken.None);
            return (null, "Не удалось отправить письмо с кодом. Проверьте адрес и попробуйте ещё раз.");
        }

        return (new RegistrationPendingResponse
        {
            Email = normalizedEmail,
            CodeExpiresInSeconds = ConfirmationLifetimeMinutes * 60
        }, null);
    }

    public async Task<UserEntity?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    public string CreateSessionToken(UserEntity user)
    {
        if (!user.IsEmailConfirmed)
        {
            throw new InvalidOperationException("Нельзя создать сессию для неподтверждённого email.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),
            new("email_verified", "true"),
            new("race", user.Race),
            new("username", user.Username ?? string.Empty),
            new("platform_role", user.PlatformRole)
        };
        AppendRoleClaims(claims, user);

        var keyValue = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(keyValue))
        {
            throw new InvalidOperationException("Jwt:Key is not configured.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var lifetimeDays = int.TryParse(_configuration["Jwt:SessionDays"], out var days) ? days : 7;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(lifetimeDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public AuthUserResponse MapUserResponse(UserEntity user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            AvatarEmoji = user.AvatarEmoji,
            Race = user.Race,
            PlatformRole = _adminAccessService.IsOwnerUser(user) ? PlatformRoles.Owner : user.PlatformRole,
            IsOwner = _adminAccessService.IsOwnerUser(user),
            IsPlatformAdmin = _adminAccessService.IsOwnerUser(user)
                || PlatformRoles.IsAdminOrOwner(user.PlatformRole)
                || _adminAccessService.IsPrivilegedUser(user.Username, user.Email),
            IsBannedFromCommenting = user.IsBannedFromCommenting,
            IsEmailConfirmed = user.IsEmailConfirmed
        };

    public async Task<(bool Success, string? Error)> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCode = token?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedEmail)
            || normalizedCode.Length != 6
            || normalizedCode.Any(character => !char.IsAsciiDigit(character)))
        {
            return (false, "Неверный код подтверждения.");
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            return (false, "Код недействителен или истёк.");
        }

        // Never turn the confirmation endpoint into a passwordless login for an
        // already active account. Confirmed users must use the regular sign-in flow.
        if (user.IsEmailConfirmed)
        {
            return (false, "Email уже подтверждён. Войдите с помощью email и пароля.");
        }

        if (string.IsNullOrWhiteSpace(user.EmailConfirmationToken)
            || user.EmailConfirmationExpiresAtUtc is null
            || user.EmailConfirmationExpiresAtUtc <= DateTime.UtcNow)
        {
            return (false, "Код недействителен или истёк. Запросите новый код.");
        }

        if (user.EmailConfirmationFailedAttempts >= MaxConfirmationAttempts)
        {
            return (false, "Слишком много неверных попыток. Запросите новый код.");
        }

        var isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(normalizedCode, user.EmailConfirmationToken);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Treat malformed legacy token values as invalid rather than leaking an error.
        }

        if (!isValid)
        {
            user.EmailConfirmationFailedAttempts++;
            if (user.EmailConfirmationFailedAttempts >= MaxConfirmationAttempts)
            {
                user.EmailConfirmationToken = null;
                user.EmailConfirmationExpiresAtUtc = null;
            }

            await _userRepository.SaveChangesAsync(user, cancellationToken);
            return (false, user.EmailConfirmationFailedAttempts >= MaxConfirmationAttempts
                ? "Слишком много неверных попыток. Запросите новый код."
                : "Неверный код подтверждения.");
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationExpiresAtUtc = null;
        user.EmailConfirmationLastSentAtUtc = null;
        user.EmailConfirmationFailedAttempts = 0;
        await _userRepository.SaveChangesAsync(user, cancellationToken);

        // Реферальная система BronyVPN: если приглашённый подтвердил email —
        // начисляем пригласившему бонусные дни за успешного реферала.
        if (user.ReferredByUserId.HasValue && user.ReferredByUserId.Value != user.Id)
        {
            var referrer = await _userRepository.GetByIdAsync(user.ReferredByUserId.Value, cancellationToken);
            if (referrer != null)
            {
                await _vpnRepository.AddReferralRewardAsync(
                    new ReferralRewardEntity
                    {
                        Id = Guid.NewGuid(),
                        ReferrerId = referrer.Id,
                        ReferralUserId = user.Id,
                        BonusDays = 7,
                        Reason = "email_confirmed",
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    cancellationToken);
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResendEmailConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return (false, "Укажите корректный email.");
        }

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // A generic success prevents this public endpoint from being used to enumerate accounts.
        if (user == null || user.IsEmailConfirmed)
        {
            return (true, null);
        }

        var now = DateTime.UtcNow;
        if (user.EmailConfirmationLastSentAtUtc is { } lastSent)
        {
            var waitSeconds = ConfirmationResendCooldownSeconds - (int)(now - lastSent).TotalSeconds;
            if (waitSeconds > 0)
            {
                return (false, $"Новый код можно запросить через {waitSeconds} сек.");
            }
        }

        var confirmationCode = CreateEmailConfirmationCode();
        user.EmailConfirmationToken = BCrypt.Net.BCrypt.HashPassword(confirmationCode);
        user.EmailConfirmationExpiresAtUtc = now.AddMinutes(ConfirmationLifetimeMinutes);
        user.EmailConfirmationLastSentAtUtc = now;
        user.EmailConfirmationFailedAttempts = 0;
        await _userRepository.SaveChangesAsync(user, cancellationToken);

        try
        {
            await _emailService.SendEmailConfirmationAsync(
                normalizedEmail,
                confirmationCode,
                CancellationToken.None);
        }
        catch (Exception)
        {
            user.EmailConfirmationLastSentAtUtc = null;
            await _userRepository.SaveChangesAsync(user, CancellationToken.None);
            return (false, "Не удалось отправить письмо. Попробуйте позже.");
        }

        return (true, null);
    }

    public async Task<(AuthUserResponse? Response, string? Error)> UpdateUsernameAsync(
        Guid userId,
        string username,
        CancellationToken cancellationToken = default)
    {
        if (!UsernameRules.TryNormalize(username, out var normalized, out var validationError))
        {
            return (null, validationError);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.");
        }

        if (string.Equals(user.Username, normalized, StringComparison.Ordinal))
        {
            return (MapUserResponse(user), null);
        }

        if (await _userRepository.UsernameExistsForOtherUserAsync(normalized, userId, cancellationToken))
        {
            return (null, "Этот юзернейм уже занят.");
        }

        user.Username = normalized;
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (MapUserResponse(user), null);
    }

    public async Task<(bool Success, string? Error)> UpdatePasswordAsync(
        Guid userId,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordRules.TryValidateChange(newPassword, confirmPassword, out var validationError))
        {
            return (false, validationError);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (false, "Пользователь не найден.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (true, null);
    }

    public async Task<(AuthUserResponse? Response, string? Error)> UpdateAvatarEmojiAsync(
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        if (!EmojiRules.TryNormalize(emoji, out var normalized, out var validationError))
        {
            return (null, validationError);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.");
        }

        user.AvatarEmoji = normalized;
        await _userRepository.SaveChangesAsync(user, cancellationToken);
        return (MapUserResponse(user), null);
    }

    private void AppendRoleClaims(List<Claim> claims, UserEntity user)
    {
        claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.User));

        if (_adminAccessService.IsOwnerUser(user) || PlatformRoles.IsOwner(user.PlatformRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Owner));
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
            return;
        }

        if (string.Equals(user.PlatformRole, PlatformRoles.Admin, StringComparison.Ordinal)
            || _adminAccessService.IsPrivilegedUser(user.Username, user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
        }
    }

    private static string CreateEmailConfirmationCode() =>
    RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    // Генерирует уникальный (пока ещё не занятый) реферальный код.
    private async Task<string> GenerateUniqueReferralCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = Models.VpnConfig.GeneratePromoCode();
            if (!await _userRepository.ReferralCodeExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        // Крайне маловероятно, но на всякий случай не даём зациклиться.
        return Models.VpnConfig.GeneratePromoCode() + Guid.NewGuid().ToString("N")[..4];
    }

    private static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
}
