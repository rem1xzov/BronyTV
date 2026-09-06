using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;

namespace BronyTV.Service;

public class NewsCommentService : INewsCommentService
{
    private readonly INewsCommentRepository _repository;
    private readonly INewsPostRepository _newsRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStreakRepository _streakRepository;

    public NewsCommentService(
        INewsCommentRepository repository,
        INewsPostRepository newsRepository,
        IUserRepository userRepository,
        IStreakRepository streakRepository)
    {
        _repository = repository;
        _newsRepository = newsRepository;
        _userRepository = userRepository;
        _streakRepository = streakRepository;
    }

    public async Task<IReadOnlyList<NewsCommentResponse>> GetCommentsAsync(
        Guid newsId,
        CancellationToken cancellationToken = default)
    {
        var comments = await _repository.GetByNewsIdAsync(newsId, cancellationToken);
        var summaries = await GetStreakSummariesAsync(comments.Select(comment => comment.AuthorId), cancellationToken);
        return comments.Select(comment => CommentToResponse(comment, summaries)).ToList();
    }

    public async Task<(NewsCommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid newsId,
        Guid authorId,
        string content,
        List<string>? images,
        Guid? replyToCommentId,
        CancellationToken cancellationToken = default)
    {
        var hasContent = !string.IsNullOrWhiteSpace(content);
        var hasImages = images != null && images.Count > 0;

        if (!hasContent && !hasImages)
        {
            return (null, "Сообщение не может быть пустым.", 400);
        }

        if (content.Length > 4000)
        {
            return (null, "Сообщение слишком длинное.", 400);
        }

        var news = await _newsRepository.GetByIdAsync(newsId, cancellationToken);
        if (news == null)
        {
            return (null, "Новость не найдена.", 404);
        }

        var user = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", 404);
        }

        NewsCommentEntity? replyToComment = null;
        if (replyToCommentId.HasValue)
        {
            replyToComment = await _repository.GetByIdAsync(replyToCommentId.Value, cancellationToken);
            if (replyToComment == null)
            {
                return (null, "Комментарий, на который вы отвечаете, не найден.", 404);
            }
        }

        var comment = new NewsCommentEntity
        {
            Id = Guid.NewGuid(),
            NewsId = newsId,
            Content = content.Trim(),
            AuthorId = authorId,
            ReplyToCommentId = replyToCommentId,
            CreatedAtUtc = DateTime.UtcNow,
            Images = images != null ? JsonSerializer.Serialize(images) : null
        };

        await _repository.AddAsync(comment, cancellationToken);

        var authorSummaries = await GetStreakSummariesAsync(new[] { authorId }, cancellationToken);
        var (authorStreak, authorStreakActive) = ResolveStreak(authorSummaries, authorId);

        var response = new NewsCommentResponse
        {
            Id = comment.Id,
            Content = comment.Content,
            AuthorUsername = user.Username ?? "unknown",
            AuthorRole = user.PlatformRole ?? "user",
            CreatedAtUtc = comment.CreatedAtUtc,
            Images = DeserializeImages(comment.Images),
            Likes = 0,
            LikedByMe = false,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToAuthorUsername = replyToComment?.Author?.Username ?? "unknown",
            ReplyToContent = replyToComment?.Content,
            AuthorStreak = authorStreak,
            AuthorStreakActive = authorStreakActive
        };

        return (response, null, 201);
    }

    public async Task<(NewsCommentResponse? Response, string? Error, int StatusCode)> ToggleLikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _repository.GetByIdAsync(commentId, cancellationToken);
        if (comment == null)
        {
            return (null, "Комментарий не найден.", 404);
        }

        var likedIds = DeserializeLikedUserIds(comment.LikedUserIds);
        var currentIdStr = userId.ToString();
        bool likedByMe;

        if (likedIds.Contains(currentIdStr))
        {
            likedIds.Remove(currentIdStr);
            likedByMe = false;
        }
        else
        {
            likedIds.Add(currentIdStr);
            likedByMe = true;
        }

        comment.LikedUserIds = likedIds.Count > 0 ? JsonSerializer.Serialize(likedIds) : null;
        await _repository.UpdateAsync(comment, cancellationToken);

        var authorSummaries = await GetStreakSummariesAsync(new[] { comment.AuthorId }, cancellationToken);
        var (authorStreak, authorStreakActive) = ResolveStreak(authorSummaries, comment.AuthorId);

        var response = new NewsCommentResponse
        {
            Id = comment.Id,
            Content = comment.Content ?? string.Empty,
            AuthorUsername = comment.Author?.Username ?? "unknown",
            AuthorRole = comment.Author?.PlatformRole ?? "user",
            CreatedAtUtc = comment.CreatedAtUtc,
            Images = DeserializeImages(comment.Images),
            Likes = likedIds.Count,
            LikedByMe = likedByMe,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToAuthorUsername = comment.ReplyToComment?.Author?.Username ?? "unknown",
            ReplyToContent = comment.ReplyToComment?.Content,
            AuthorStreak = authorStreak,
            AuthorStreakActive = authorStreakActive
        };

        return (response, null, 200);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteCommentAsync(
        Guid commentId,
        Guid userId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var comment = await _repository.GetByIdAsync(commentId, cancellationToken);
        if (comment == null)
        {
            return (false, "Комментарий не найден.", 404);
        }

        var authorRole = comment.Author?.PlatformRole ?? "user";

        if (currentUserRole == "owner")
        {
            await _repository.DeleteAsync(comment, cancellationToken);
            return (true, null, 204);
        }

        if (comment.AuthorId == userId)
        {
            await _repository.DeleteAsync(comment, cancellationToken);
            return (true, null, 204);
        }

        if (currentUserRole == "admin" && authorRole != "owner")
        {
            await _repository.DeleteAsync(comment, cancellationToken);
            return (true, null, 204);
        }

        return (false, "Недостаточно прав для удаления комментария.", 403);
    }

    private static NewsCommentResponse CommentToResponse(
        NewsCommentEntity comment,
        IReadOnlyDictionary<Guid, StreakSummary> summaries)
    {
        var likedIds = DeserializeLikedUserIds(comment.LikedUserIds);
        var (authorStreak, authorStreakActive) = ResolveStreak(summaries, comment.AuthorId);
        return new NewsCommentResponse
        {
            Id = comment.Id,
            Content = comment.Content ?? string.Empty,
            AuthorUsername = comment.Author?.Username ?? "unknown",
            AuthorRole = comment.Author?.PlatformRole ?? "user",
            CreatedAtUtc = comment.CreatedAtUtc,
            Images = DeserializeImages(comment.Images),
            Likes = likedIds.Count,
            LikedByMe = false,
            ReplyToCommentId = comment.ReplyToCommentId,
            ReplyToAuthorUsername = comment.ReplyToComment?.Author?.Username ?? "unknown",
            ReplyToContent = comment.ReplyToComment?.Content,
            AuthorStreak = authorStreak,
            AuthorStreakActive = authorStreakActive
        };
    }

    private async Task<IReadOnlyDictionary<Guid, StreakSummary>> GetStreakSummariesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, StreakSummary>();
        }

        return await _streakRepository.GetStreakSummariesAsync(
            ids,
            DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);
    }

    private static (int Streak, bool Active) ResolveStreak(
        IReadOnlyDictionary<Guid, StreakSummary> summaries,
        Guid userId)
    {
        return summaries.TryGetValue(userId, out var summary)
            ? (summary.CurrentStreak, summary.IsCreditedToday)
            : (0, false);
    }

    private static List<string>? DeserializeImages(string? imagesJson)
    {
        if (string.IsNullOrWhiteSpace(imagesJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(imagesJson);
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> DeserializeLikedUserIds(string? likedUserIdsJson)
    {
        if (string.IsNullOrWhiteSpace(likedUserIdsJson))
        {
            return new HashSet<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(likedUserIdsJson);
            return list != null ? new HashSet<string>(list) : new HashSet<string>();
        }
        catch
        {
            return new HashSet<string>();
        }
    }
}
