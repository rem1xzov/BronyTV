using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private const long MaxUploadSizeBytes = 500L * 1024 * 1024;
    private readonly IVideoService _videoService;

    public VideoController(IVideoService videoService)
    {
        _videoService = videoService;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadSizeBytes)]
    public async Task<IActionResult> Upload([FromForm] UploadVideoRequest request)
    {
        if (request.SeasonId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.SeasonId), "SeasonId is required.");
        }

        if (request.VideoFile is null || request.VideoFile.Length == 0)
        {
            ModelState.AddModelError(nameof(request.VideoFile), "Video file is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _videoService.UploadVideoAsync(request);
        return Ok(result);
    }

    [HttpGet("season/{number}")]
    public async Task<IActionResult> GetBySeason(int number)
    {
        var videos = await _videoService.GetVideosBySeasonAsync(number);
        return Ok(videos);
    }
}