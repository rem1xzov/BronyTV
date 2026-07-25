using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Models;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAccessService _adminAccessService;

    public UsersController(
        IAdminUserService adminUserService,
        IUserRepository userRepository,
        IAdminAccessService adminAccessService)
    {
        _adminUserService = adminUserService;
        _userRepository = userRepository;
        _adminAccessService = adminAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var users = await _adminUserService.ListUsersAsync(page, pageSize, cancellationToken);
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest();
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            return BadRequest(new { message = "Укажите корректный email адрес." });
        }

        if (!UsernameRules.TryNormalize(request.Username, out var normalizedUsername, out var usernameError))
        {
            return BadRequest(new { message = usernameError });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { message = "Пароль должен быть не менее 8 символов." });
        }

        if (request.Role != PlatformRoles.User && request.Role != PlatformRoles.Admin && request.Role != PlatformRoles.Owner)
        {
            return BadRequest(new { message = "Недопустимая роль." });
        }

        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return BadRequest(new { message = "Пользователь с таким email уже зарегистрирован." });
        }

        if (await _userRepository.UsernameExistsAsync(normalizedUsername, cancellationToken))
        {
            return BadRequest(new { message = "Этот юзернейм уже занят." });
        }

        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = normalizedUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Race = string.IsNullOrWhiteSpace(request.Race) ? "earth_pony" : request.Race.Trim().ToLowerInvariant(),
            CreatedAtUtc = now,
            RaceSelectedAtUtc = now,
            IsBannedFromCommenting = false,
            PlatformRole = request.Role
        };

        await _userRepository.CreateAsync(user, cancellationToken);

        // Map response
        var isOwner = _adminAccessService.IsOwnerUser(user);
        var response = new AdminUserSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Race = user.Race,
            Role = isOwner ? PlatformRoles.Owner : user.PlatformRole,
            IsOwner = isOwner,
            IsBannedFromCommenting = user.IsBannedFromCommenting,
            CreatedAtUtc = user.CreatedAtUtc
        };

        return CreatedAtAction(nameof(ListUsers), new { id = user.Id }, response);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actingAdminUserId))
        {
            return Unauthorized();
        }

        var (success, error, statusCode) = await _adminUserService.DeleteUserAsync(
            userId,
            actingAdminUserId,
            cancellationToken);

        if (!success)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return NoContent();
    }

    [HttpPatch("{userId:guid}")]
    public async Task<IActionResult> PatchUser(Guid userId, [FromBody] PatchUserRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest();
        }

        var user = await _userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "Пользователь не найден." });
        }

        if (_adminAccessService.IsProtectedOwner(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Нельзя изменять роль или банить владельца платформы." });
        }

        if (request.Role != null)
        {
            if (request.Role != PlatformRoles.User && request.Role != PlatformRoles.Admin && request.Role != PlatformRoles.Owner)
            {
                return BadRequest(new { message = "Недопустимая роль." });
            }
            user.PlatformRole = request.Role;
        }

        if (request.IsBannedFromCommenting.HasValue)
        {
            user.IsBannedFromCommenting = request.IsBannedFromCommenting.Value;
        }

        await _userRepository.SaveChangesAsync(user, cancellationToken);

        var isOwner = _adminAccessService.IsOwnerUser(user);
        var response = new AdminUserSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Race = user.Race,
            Role = isOwner ? PlatformRoles.Owner : user.PlatformRole,
            IsOwner = isOwner,
            IsBannedFromCommenting = user.IsBannedFromCommenting,
            CreatedAtUtc = user.CreatedAtUtc
        };

        return Ok(response);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
