using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public record UploadVideoRequest(
    [Required, StringLength(255, MinimumLength = 1)] string Title,
    [Range(1, int.MaxValue)] int EpisodeNumber,
    Guid SeasonId,
    [Required] IFormFile VideoFile,
    IFormFile? PreviewFile,
    [Required, StringLength(4000, MinimumLength = 1)] string Description);