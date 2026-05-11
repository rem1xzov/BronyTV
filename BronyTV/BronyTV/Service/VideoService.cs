using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Models;
using BronyTV.Repository;

namespace BronyTV.Service;

public class VideoService : IVideoService
{
    private static readonly string[] VideoFolderSegments = ["content", "video"];
    private static readonly string[] PreviewFolderSegments = ["content", "previews"];
    private readonly IVideoRepository _videoRepository;
    private readonly IWebHostEnvironment _env;

    public VideoService(IVideoRepository videoRepository, IWebHostEnvironment env)
    {
        _videoRepository = videoRepository;
        _env = env;
    }

    public async Task<Video> UploadVideoAsync(UploadVideoRequest request)
    {
        var savedVideoPath = await SaveFileAsync(request.VideoFile, VideoFolderSegments);
        if (string.IsNullOrEmpty(savedVideoPath))
        {
            throw new InvalidOperationException("Не удалось сохранить видеофайл.");
        }

        string videoPath = savedVideoPath;
        string? previewPath = request.PreviewFile != null
            ? await SaveFileAsync(request.PreviewFile, PreviewFolderSegments)
            : null;

        var video = new VideoEntity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            EpisodeNumber = request.EpisodeNumber,
            Description = request.Description,
            SeasonId = request.SeasonId,
            FilePath = videoPath,
            PreviewImageUrl = previewPath,
        };
        await _videoRepository.AddVideoAsync(video);
        return new Video
        {
            Id = video.Id,
            Title = request.Title,
            EpisodeNumber = request.EpisodeNumber,
            Description = request.Description,
            FilePath = video.FilePath,
            PreviewImageUrl = video.PreviewImageUrl,
            SeasonId = request.SeasonId
        };
    }
    private async Task<string?> SaveFileAsync(IFormFile file, params string[] folderSegments)
    {
        if (file == null || file.Length == 0) return null;

        var webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var directoryPath = Path.Combine([webRootPath, ..folderSegments]);
        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, fileName);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return "/" + string.Join('/', folderSegments.Append(fileName));
    }

    public async Task<IEnumerable<Video>> GetVideosBySeasonAsync(int seasonNumber)
    {
        var entities = await _videoRepository.GetVideosBySeasonAsync(seasonNumber);
        return entities.Select(v => new Video
        {
            Id = v.Id,
            Title = v.Title,
            EpisodeNumber = v.EpisodeNumber,
            FilePath = v.FilePath,
            PreviewImageUrl = v.PreviewImageUrl,
            Description = v.Description,
            SeasonId = v.SeasonId
        });
    }
    
    public async Task<Video?> GetVideoByIdAsync(Guid id)
    {
        var v = await _videoRepository.GetVideoByIdAsync(id);
        if (v == null) return null;
        return new Video { Id = v.Id, Title = v.Title, FilePath = v.FilePath };
    }

    public async Task DeleteVideoAsync(Guid id) 
        => await _videoRepository.DeleteVideoAsync(id);
}