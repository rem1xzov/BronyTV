using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Стриминг видео с поддержкой HTTP Range (206) — обязательно для Safari/iOS.
/// </summary>
[ApiController]
[Route("videos")]
[AllowAnonymous]
public class VideoStreamController : ControllerBase
{
    private readonly string _videosRoot;
    private readonly IUserActivityService _userActivityService;

    public VideoStreamController(IConfiguration configuration, IUserActivityService userActivityService)
    {
        _videosRoot = configuration["VideoStorage:RootPath"]
            ?? Environment.GetEnvironmentVariable("BRONYTV_VIDEOS_ROOT")
            ?? "/app/media";
        _userActivityService = userActivityService;
    }

    [HttpGet("{**relativePath}")]
    public async Task<IActionResult> StreamVideo(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return NotFound();
        }

        var rootFull = Path.GetFullPath(_videosRoot);
        if (!Directory.Exists(rootFull))
        {
            return NotFound();
        }

        var relative = relativePath.Replace('\\', '/').TrimStart('/');
        var safePath = Path.GetFullPath(Path.Combine(rootFull, relative));

        if (!safePath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            || !System.IO.File.Exists(safePath))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(safePath).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".m4v" => "video/x-m4v",
            _ => "application/octet-stream"
        };

        // Логируем просмотр серии для залогиненных пользователей (гости не пишутся в историю).
        // Запись идём от пути /сезон N/имя.mp4, из которого вытаскиваем сезон и серию.
        await LogVideoWatchIfAuthenticatedAsync(relative, cancellationToken: default);

        return PhysicalFile(safePath, contentType, enableRangeProcessing: true);
    }

    private async Task LogVideoWatchIfAuthenticatedAsync(string relative, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        // Сезон в имени папки: ".../сезон 3/s1e2.mp4"
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? seasonPart = null;
        string? fileName = null;
        if (segments.Length >= 2)
        {
            seasonPart = segments[^2];
            fileName = segments[^1];
        }
        else if (segments.Length == 1)
        {
            fileName = segments[0];
        }

        var numberRuns = new Regex(@"\d+", RegexOptions.CultureInvariant);
        var seasonMatch = numberRuns.Match(seasonPart ?? string.Empty);

        // Ищем номер серии в имени файла (по той же логике, что и синхронизация с диска):
        // например "s1e2.mp4" → 2, иначе первое число.
        int? season = seasonMatch.Success && int.TryParse(seasonMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? s
            : (int?)null;

        int? episode = null;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var numbers = numberRuns.Matches(fileName)
                .Cast<Match>()
                .Select(m => int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (int?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
            if (numbers.Count >= 2)
            {
                episode = numbers[1];
            }
            else if (numbers.Count == 1)
            {
                episode = numbers[0];
            }
        }

        string details;
        if (season.HasValue && episode.HasValue)
        {
            details = $"Сезон {season.Value} — серия {episode.Value}";
        }
        else if (season.HasValue)
        {
            details = $"Сезон {season.Value}";
        }
        else if (episode.HasValue)
        {
            details = $"Серия {episode.Value}";
        }
        else
        {
            return; // Не смогли понять, какую серию открыли — пропускаем.
        }

        await _userActivityService.RecordAsync(userId, "video_watch", details, cancellationToken);
    }
}
