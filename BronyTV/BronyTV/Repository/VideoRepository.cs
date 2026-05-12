using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using BronyTV.Models;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.Repository;

public class VideoRepository : IVideoRepository
{
    private readonly DbBronyTV _context;

    public VideoRepository(DbBronyTV context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Video>> GetAllVideosAsync()
    {
        var entities = await _context.Videos.Include(v => v.Season).ToListAsync();

        return entities.Select(v => new Video
        {
            Id = v.Id,
            Title = v.Title,
            EpisodeNumber = v.EpisodeNumber,
            FilePath = v.FilePath,
            PreviewImageUrl = v.PreviewImageUrl,
            SeasonId = v.SeasonId
        });
    }

    public async Task<VideoEntity?> GetVideoByIdAsync(Guid Id)
    {
        return await _context.Videos.Include(v => v.Season).FirstOrDefaultAsync(v => v.Id == Id);
    }

    public async Task<IEnumerable<VideoEntity>> GetVideosBySeasonAsync(int seasonNumber)
    {
        return await _context.Videos
            .Where(v => v.Season.Number == seasonNumber)
            .ToListAsync();
    }

    public async Task AddVideoAsync(VideoEntity video)
    {
        await _context.Videos.AddAsync(video);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteVideoAsync(Guid Id)
    {
        var video = await _context.Videos.FindAsync(Id);
        if (video != null)
        {
            _context.Videos.Remove(video);
            await _context.SaveChangesAsync();
        }
    }
}