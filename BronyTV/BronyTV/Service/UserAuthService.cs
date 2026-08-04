using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace BronyTV.Service;

public class UserAuthService : IUserAuthService
{
        private readonly IUserRepository _userRepository;
    private readonly IAdminAccessService _adminAccessService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public UserAuthService(
        IUserRepository userRepository,
        IAdminAccessService adminAccessService,
        IEmailService emailService,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _userRepository = userRepository;
        _adminAccessService = adminAccessService;
        _emailService = emailService;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<(AuthUserResponse? Response, string? Error)> RegisterAsync(
        string email,
        string password,
        string race,
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return (null, "Укажите корректный email.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return (null, "Пароль должен содержать минимум 8 символов.");
        }

        if (!UserRace.TryNormalize(race, out var normalizedRace))
        {
            return (null, "Выберите расу: пегасы, единороги или земные пони.");
        }

        if (!UsernameRules.TryNormalize(username, out var normalizedUsername, out var usernameError))
        {
            return (null, usernameError);
        }

                if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return (null, "Пользователь с таким email уже зарегистрирован.");
        }

        if (await _userRepository.UsernameExistsAsync(normalizedUsername, cancellationToken))
        {
            return (null, "Этот юзернейм уже занят");
        }

        // Do NOT write to the Users table yet. Only an in-memory pending record is kept so
        // that fake/unconfirmed registrations never clutter the database.
        var code = CreateEmailConfirmationCode();
        var pending = new PendingRegistration
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Username = normalizedUsername,
            Race = normalizedRace,
            Code = code,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(PendingLifetimeMinutes)
        };

        _cache.Set(PendingKey(normalizedEmail), pending, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = pending.ExpiresUtc
        });

        // Try to send the confirmation email. Failures are logged (inside EmailService)
        // but do not block registration.
        try
        {
            await _emailService.SendEmailConfirmationAsync(normalizedEmail, code, CancellationToken.None);
        }
        catch (Exception)
        {
            // Best-effort: registration succeeds even if the mail provider is unavailable.
        }

        return (new AuthUserResponse
        {
            Email = normalizedEmail,
            Username = normalizedUsername,
            Race = normalizedRace,
            PlatformRole = "User",
            IsEmailConfirmed = false
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

        var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return valid ? user : null;
    }

    public string CreateSessionToken(UserEntity user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),
            new("race", user.Race),
            new("username", user.Username ?? string.Empty),
            new("platform_role", user.PlatformRole)
        };
        AppendRoleClaims(claims, user);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var lifetimeDays = int.TryParse(_configuration["Jwt:SessionDays"], out var days) ? days : 7;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(lifetimeDays),
            signingCredentials: creds);

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
        if (string.IsNullOrEmpty(normalizedEmail) || string.IsNullOrWhiteSpace(token))
        {
            return (false, "Неверный код подтверждения.");
        }

                // Already an active (confirmed) user? Nothing else to do.
        var existing = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing != null && existing.IsEmailConfirmed)
        {
            return (true, null);
        }

        // Resolve the pending (unconfirmed) registration from the in-memory cache.
        var pending = _cache.Get<PendingRegistration>(PendingKey(normalizedEmail));
        if (pending == null)
        {
            return (false, "Код недействителен или истёк. Запросите новый код или зарегистрируйтесь заново.");
        }

        if (pending.ExpiresUtc < DateTime.UtcNow)
        {
            _cache.Remove(PendingKey(normalizedEmail));
            return (false, "Время действия кода истекло. Зарегистрируйтесь заново.");
        }

        if (!string.Equals(pending.Code, token.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Код подтверждения недействителен или устарел. Запросите новое письмо.");
        }

        // Re-check uniqueness in case the email/username was taken while waiting for confirmation.
        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return (false, "Этот email уже занят другим пользователем.");
        }

        var username = pending.Username ?? string.Empty;
        if (!string.IsNullOrEmpty(username)
            && await _userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            return (false, "Этот юзернейм уже занят. Зарегистрируйтесь заново с другим.");
        }

        // Confirmation is valid — now actually create the real user in the database.
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = pending.Username,
            PasswordHash = pending.PasswordHash,
            Race = pending.Race,
            CreatedAtUtc = now,
            RaceSelectedAtUtc = now,
            IsBannedFromCommenting = false,
            PlatformRole = _adminAccessService.ResolveInitialRoleForUsername(username),
            IsEmailConfirmed = true,
            EmailConfirmationToken = null
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        _cache.Remove(PendingKey(normalizedEmail));
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

                var pending = _cache.Get<PendingRegistration>(PendingKey(normalizedEmail));
        if (pending == null)
        {
            return (false, "Активная регистрация для этого email не найдена. Зарегистрируйтесь заново.");
        }

        // If the email was confirmed meanwhile in the DB, do not regenerate.
        var existing = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing != null && existing.IsEmailConfirmed)
        {
            return (false, "Email уже подтверждён.");
        }

        pending.Code = CreateEmailConfirmationCode();
        pending.ExpiresUtc = DateTime.UtcNow.AddMinutes(PendingLifetimeMinutes);
        _cache.Set(PendingKey(normalizedEmail), pending, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = pending.ExpiresUtc
        });

        try
        {
            await _emailService.SendEmailConfirmationAsync(normalizedEmail, pending.Code, CancellationToken.None);
        }
        catch (Exception)
        {
            return (false, "Не удалось отправить письмо. Попробуйте позже.");
        }

                return (true, null);
    }

    public Task<bool> TryResendPendingConfirmationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var pending = _cache.Get<PendingRegistration>(PendingKey(normalizedEmail));
        if (pending == null)
        {
            return Task.FromResult(false);
        }

        pending.Code = CreateEmailConfirmationCode();
        pending.ExpiresUtc = DateTime.UtcNow.AddMinutes(PendingLifetimeMinutes);
        _cache.Set(PendingKey(normalizedEmail), pending, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = pending.ExpiresUtc
        });

        // Fire-and-forget send; SignIn only needs to know a code was issued so it can
        // return 409 "requiresEmailConfirmation" and switch the UI to the code screen.
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendEmailConfirmationAsync(normalizedEmail, pending.Code, CancellationToken.None);
            }
            catch (Exception)
            {
                // Best-effort: failures are already logged inside EmailService.
            }
        });

        return Task.FromResult(true);
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
            return (null, "Этот юзернейм уже занят");
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

        if (_adminAccessService.IsOwnerUser(user))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Owner));
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
            return;
        }

        if (PlatformRoles.IsOwner(user.PlatformRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Owner));
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
            return;
        }

        if (string.Equals(user.PlatformRole, PlatformRoles.Admin, StringComparison.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
            return;
        }

                        if (_adminAccessService.IsPrivilegedUser(user.Username, user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformRoles.Admin));
        }
    }

        private static string CreateEmailConfirmationCode() =>
        Random.Shared.Next(100000, 1000000).ToString("D6");

    private static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private const int PendingLifetimeMinutes = 15;

    private static string PendingKey(string email) => $"pending-registration:{email}";

    private sealed class PendingRegistration
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string Race { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }
}
