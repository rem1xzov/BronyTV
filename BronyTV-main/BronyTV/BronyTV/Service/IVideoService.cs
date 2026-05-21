using BronyTV.Contract;
using BronyTV.Models;

namespace BronyTV.Service;

public interface IVideoService
{
    Task<IEnumerable<Video>> GetVideosBySeasonAsync(int seasonNumber);
    Task<Video?> GetVideoByIdAsync(Guid id);
    
    Task<Video> UploadVideoAsync(UploadVideoRequest request);
    
    Task DeleteVideoAsync(Guid id);
}