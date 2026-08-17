using System.Security.Claims;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly IUserActivityService _userActivityService;

    public AdminController(
        IAdminUserService adminUserService,
        IUserActivityService userActivityService)
    {
        _adminUserService = adminUserService;
        _userActivityService = userActivityService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var users = await _adminUserService.ListUsersAsync(page, pageSize, cancellationToken);
        return Ok(users);
    }

        /// <summary>
    /// История последних действий пользователя (последние 10 записей, по убыванию времени).
    /// Доступно только владельцу/администратору.
    /// </summary>
    [HttpGet("users/{userId:guid}/activity")]
    public async Task<IActionResult> GetUserActivity(Guid userId, CancellationToken cancellationToken)
    {
        var response = await _userActivityService.GetRecentAsync(userId, 10, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Активность ВСЕХ пользователей за последние 7 дней (единая хронологическая лента).
    /// Используется отдельной страницей «Активность» в админке.
    /// Параметр <paramref name="days"/> позволяет расширить/сузить окно (по умолчанию 7).
    /// </summary>
    [HttpGet("activity/week")]
    public async Task<IActionResult> GetRecentAllUsersActivity(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var items = await _userActivityService.GetRecentAllUsersAsync(days, cancellationToken);
        return Ok(new { activities = items });
    }


    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<object>());
        }

        var users = await _adminUserService.SearchUsersAsync(query.Trim(), cancellationToken);
        return Ok(users);
    }

    [HttpDelete("users/{userId:guid}")]
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

    [HttpPut("users/{userId:guid}/toggle-comment-ban")]
    public async Task<IActionResult> ToggleCommentBan(Guid userId, CancellationToken cancellationToken)
    {
        var (response, error, statusCode) = await _adminUserService.ToggleCommentBanAsync(
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [HttpPut("users/{userId:guid}/promote-admin")]
    public async Task<IActionResult> PromoteAdmin(Guid userId, CancellationToken cancellationToken)
    {
        var (response, error, statusCode) = await _adminUserService.PromoteToAdminAsync(
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [HttpPut("users/{userId:guid}/demote-admin")]
    public async Task<IActionResult> DemoteAdmin(Guid userId, CancellationToken cancellationToken)
    {
        var (response, error, statusCode) = await _adminUserService.DemoteFromAdminAsync(
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
