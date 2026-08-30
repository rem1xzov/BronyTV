using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Анонсы совместного просмотра. Список доступен всем (гостям тоже), создание/отмена — Admin.
/// </summary>
[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    private readonly IStreamAnnouncementRepository _announcementRepository;

    public StreamController(IStreamAnnouncementRepository announcementRepository)
    {
        _announcementRepository = announcementRepository;
    }

    /// <summary>
    /// Список анонсов. <c>filter</c>: upcoming (запланированные), past (прошедшие/завершённые/отменённые),
    /// либо без фильтра — все, отсортированные по дате.
    /// </summary>
    [HttpGet("announcements")]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string? filter = null, CancellationToken cancellationToken = default)
    {
        var entities = await _announcementRepository.ListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var items = entities.Select(ToResponse).ToList();

        IEnumerable<StreamAnnouncementResponse> result = filter?.ToLowerInvariant() switch
        {
            "upcoming" => items.Where(a => a.ScheduledAtUtc >= now && a.Status == "scheduled"),
            "past" => items.Where(a => a.ScheduledAtUtc < now || a.Status != "scheduled"),
            _ => items
        };

        return Ok(result.OrderBy(a => a.ScheduledAtUtc).ToList());
    }

    [HttpPost("announcements")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateStreamAnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (request.VideoId == Guid.Empty)
        {
            return BadRequest(new { message = "Укажите видео." });
        }

        if (request.ScheduledAtUtc <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new { message = "Время старта должно быть в будущем." });
        }

        Guid? adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : null;

        var announcement = new StreamAnnouncementEntity
        {
            Id = Guid.NewGuid(),
            VideoId = request.VideoId,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Status = "scheduled",
            CreatedByAdminId = adminId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _announcementRepository.CreateAsync(announcement, cancellationToken);
        return Ok(ToResponse(announcement));
    }

    [HttpPost("announcements/{id:guid}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _announcementRepository.CancelAsync(id, cancellationToken);
        return ok ? Ok() : NotFound();
    }

    private static StreamAnnouncementResponse ToResponse(StreamAnnouncementEntity entity)
    {
        return new StreamAnnouncementResponse
        {
            Id = entity.Id,
            VideoId = entity.VideoId,
            VideoTitle = entity.Video?.Title ?? string.Empty,
            SeasonNumber = entity.Video?.Season?.Number,
            SeasonTitle = entity.Video?.Season?.Title,
            ScheduledAtUtc = entity.ScheduledAtUtc,
            Status = entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
