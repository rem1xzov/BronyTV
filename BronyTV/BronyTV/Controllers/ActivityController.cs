using System.Security.Claims;
using BronyTV.Service;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Пользовательские эндпоинты логирования активности, инициируемые с фронтенда
/// (а не server-to-server, как InternalActivityController). Доступ через обычную
/// cookie-авторизацию — текущий пользователь резолвится из HttpOnly-куки
/// bronytv_session. Гости (незалогиненные) просто молча пропускаются.
/// </summary>
[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly IUserActivityService _userActivityService;

    public ActivityController(IUserActivityService userActivityService)
    {
        _userActivityService = userActivityService;
    }

    /// <summary>
    /// Логирует факт открытия новости (разворачивания карточки). Только для залогиненных.
    /// </summary>
    [HttpPost("news-view")]
    public async Task<IActionResult> RecordNewsView(
        [FromBody] ActivityNewsViewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var viewerId))
        {
            return Ok(); // Гость — не логируем, но без ошибки.
        }

        await _userActivityService.RecordAsync(
            viewerId,
            "news_view",
            string.IsNullOrWhiteSpace(request?.Title) ? null : request.Title,
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Логирует факт клика по плашке «VPN от BronyTV». Только для залогиненных.
    /// </summary>
    [HttpPost("vpn-click")]
    public async Task<IActionResult> RecordVpnClick(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Ok(); // Гость — не логируем.
        }

        await _userActivityService.RecordAsync(
            userId,
            "vpn_click",
            "VPN",
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Логирует факт начала просмотра серии. Только для залогиненных.
    /// Details передаётся с фронтенда в человекочитаемом виде ("Сезон N — серия M").
    /// Защита от дублей — на уровне IUserActivityService (окно 5 минут).
    /// </summary>
    [HttpPost("video-watch")]
    public async Task<IActionResult> RecordVideoWatch(
        [FromBody] ActivityVideoWatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Ok(); // Гость — не логируем.
        }

        await _userActivityService.RecordAsync(
            userId,
            // "movie_watch" для категорий фильмов (сезоны 10/11), иначе "video_watch".
            string.IsNullOrWhiteSpace(request?.Type) ? "video_watch" : request.Type,
            string.IsNullOrWhiteSpace(request?.Details) ? null : request.Details,
            cancellationToken);

        return Ok();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}

public class ActivityVideoWatchRequest
{
    public string? Type { get; set; }
    public string? Details { get; set; }
}

public class ActivityNewsViewRequest
{
    public string? Title { get; set; }
}
