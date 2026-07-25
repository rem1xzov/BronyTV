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

    public VideoStreamController(IConfiguration configuration)
    {
        _videosRoot = configuration["VideoStorage:RootPath"]
            ?? Environment.GetEnvironmentVariable("BRONYTV_VIDEOS_ROOT")
            ?? "/app/media";
    }

    [HttpGet("{**relativePath}")]
    public IActionResult StreamVideo(string relativePath)
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

        return PhysicalFile(safePath, contentType, enableRangeProcessing: true);
    }
}
