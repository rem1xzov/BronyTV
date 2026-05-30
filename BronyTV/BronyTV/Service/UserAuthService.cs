using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    private readonly IUserRepository _userRepository;
    private readonly IAdminAccessService _adminAccessService;
    private readonly IConfiguration _configuration;

    public UserAuthService(
        IUserRepository userRepository,
        IAdminAccessService adminAccessService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _adminAccessService = adminAccessService;
        _configuration = configuration;
    }

    public async Task<(AuthUserResponse? Response, string? Error)> RegisterAsync(
        string email,
        string password,
        string race,
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

        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return (null, "Пользователь с таким email уже зарегистрирован.");
        }

        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Race = normalizedRace,
            CreatedAtUtc = now,
            RaceSelectedAtUtc = now
        };

        var created = await _userRepository.CreateAsync(user, cancellationToken);
        return (MapUserResponse(created), null);
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
            new(ClaimTypes.Role, "User"),
            new("race", user.Race),
            new("username", user.Username ?? string.Empty)
        };

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
            IsPlatformAdmin = _adminAccessService.IsPrivilegedUser(user.Username, user.Email)
        };

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

    private static string NormalizeEmail(string email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
}
