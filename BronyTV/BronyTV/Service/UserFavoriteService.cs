using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;

namespace BronyTV.Service;

public class UserFavoriteService : IUserFavoriteService
{
    private readonly IUserFavoriteRepository _favoriteRepository;
    private readonly IVideoRepository _videoRepository;

    public UserFavoriteService(
        IUserFavoriteRepository favoriteRepository,
        IVideoRepository videoRepository)
    {
        _favoriteRepository = favoriteRepository;
        _videoRepository = videoRepository;
    }

    public async Task<bool> IsFavoriteAsync(
        Guid userId,
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        return await _favoriteRepository.IsFavoriteAsync(userId, videoId, cancellationToken);
    }

    public async Task<IReadOnlyList<FavoriteItemResponse>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var favorites = await _favoriteRepository.GetByUserAsync(userId, cancellationToken);

        return favorites
            .Where(favorite => favorite.Video != null)
            .Select(favorite => new FavoriteItemResponse
            {
                Id = favorite.Id,
                VideoId = favorite.Video!.Id,
                Title = favorite.Video.Title,
                SeasonNumber = favorite.Video.Season?.Number,
                EpisodeNumber = favorite.Video.EpisodeNumber,
                AddedAt = favorite.AddedAt
            })
            .ToList();
    }

    public async Task AddAsync(
        Guid userId,
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await _videoRepository.GetVideoByIdAsync(videoId);
        if (video == null)
        {
            throw new InvalidOperationException("Видео не найдено.");
        }

        // Идемпотентность: повторное добавление не создаёт дубликат.
        if (await _favoriteRepository.IsFavoriteAsync(userId, videoId, cancellationToken))
        {
            return;
        }

        await _favoriteRepository.AddAsync(
            new UserFavoriteEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VideoId = videoId,
                AddedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        return await _favoriteRepository.RemoveAsync(userId, videoId, cancellationToken);
    }
}
