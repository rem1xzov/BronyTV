using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Models;
using BronyTV.Repository;
using Microsoft.IdentityModel.Tokens;

namespace BronyTV.Service;

public class UserAuthService : IUserAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IConfiguration _configuration;

    public UserAuthService(
        IUserRepository userRepository,
        IGoogleTokenValidator googleTokenValidator,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _googleTokenValidator = googleTokenValidator;
        _configuration = configuration;
    }

    public async Task<(UserEntity User, bool IsNewUser)?> AuthenticateGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var payload = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        if (payload == null)
        {
            return null;
        }

        var existing = await _userRepository.GetByGoogleSubAsync(payload.Subject, cancellationToken);
        if (existing != null)
        {
            return (existing, false);
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            GoogleSub = payload.Subject,
            Email = SanitizeEmail(payload.Email),
            DisplayName = SanitizeDisplayName(payload.Name),
            Race = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user, cancellationToken);
        return (created, true);
    }

    public string CreateSessionToken(UserEntity user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, "User")
        };

        if (!string.IsNullOrEmpty(user.Race))
        {
            claims.Add(new Claim("race", user.Race));
        }

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
            DisplayName = user.DisplayName,
            Race = user.Race,
            NeedsRaceSelection = string.IsNullOrEmpty(user.Race)
        };

    public async Task<AuthUserResponse?> SelectRaceAsync(
        Guid userId,
        string race,
        CancellationToken cancellationToken = default)
    {
        if (!UserRace.TryNormalize(race, out var normalizedRace))
        {
            return null;
        }

        var updated = await _userRepository.TrySetRaceAsync(userId, normalizedRace, cancellationToken);
        if (!updated)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user == null ? null : MapUserResponse(user);
    }

    private static string SanitizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string SanitizeDisplayName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            trimmed = trimmed[..200];
        }

        return trimmed;
    }
}
