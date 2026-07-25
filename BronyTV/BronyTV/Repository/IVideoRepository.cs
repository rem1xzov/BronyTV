using BronyTV.DbContext.Entity;
using BronyTV.Models;

namespace BronyTV.Repository;

public interface IVideoRepository
{
    Task<IEnumerable<Video>> GetAllVideosAsync();
    Task<VideoEntity?> GetVideoByIdAsync(Guid Id);
    Task<IEnumerable<VideoEntity>> GetVideosBySeasonAsync(int seasonNumber);
    Task AddVideoAsync(VideoEntity video);
    Task DeleteVideoAsync(Guid Id);
}