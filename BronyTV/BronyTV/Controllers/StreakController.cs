using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Пользовательские эндпоинты системы стриков (ежедневной активности).
/// </summary>
[ApiController]
[Route("api/streak")]
public class StreakController : ControllerBase
{
    private readonly IStreakService _streakService;

    public StreakController(IStreakService streakService)
    {
        _streakService = streakService;
    }

    /// <summary>Текущий статус стрика пользователя.</summary>
    [Authorize(Roles = "User")]
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _streakService.GetStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    /// <summary>Запись активного времени просмотра видео (секунды).</summary>
    [Authorize(Roles = "User")]
    [HttpPost("video-watch")]
    public async Task<IActionResult> RecordVideoWatch(
        [FromBody] StreakRecordMinutesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _streakService.RecordVideoWatchAsync(
            userId,
            request?.Seconds ?? 0,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Поставить заморозку на следующий день.</summary>
    [Authorize(Roles = "User")]
    [HttpPost("freeze")]
    public async Task<IActionResult> SetFreeze(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _streakService.SetFreezeAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Таблица лидеров (текущий стрик; sort=longest — по рекорду).</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard(
        [FromQuery] string sort = "current",
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _streakService.GetLeaderboardAsync(sort, limit, cancellationToken);
        return Ok(result);
    }

    /// <summary>Вращение колеса фортуны (исход решает сервер).</summary>
    [Authorize(Roles = "User")]
    [HttpPost("fortune-wheel/spin")]
    public async Task<IActionResult> SpinFortuneWheel(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _streakService.SpinFortuneWheelAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Помечает непоказанные награды как показанные (после модалки поздравления).</summary>
    [Authorize(Roles = "User")]
    [HttpPost("rewards/seen")]
    public async Task<IActionResult> MarkRewardsSeen(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _streakService.MarkRewardsSeenAsync(userId, cancellationToken);
        return Ok();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
