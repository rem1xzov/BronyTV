using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;

namespace BronyTV.Service;

public class CommentService : ICommentService
{
    private const int MaxCommentLength = 500;
    private readonly ICommentRepository _commentRepository;
    private readonly IUserRepository _userRepository;

    public CommentService(ICommentRepository commentRepository, IUserRepository userRepository)
    {
        _commentRepository = commentRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<CommentResponse>> GetCommentsForVideoAsync(
        Guid videoId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _commentRepository.VideoExistsAsync(videoId, cancellationToken))
        {
            return Array.Empty<CommentResponse>();
        }

        var comments = await _commentRepository.GetByVideoIdAsync(videoId, cancellationToken);
        if (comments.Count == 0)
        {
            return Array.Empty<CommentResponse>();
        }

        var commentIds = comments.Select(comment => comment.Id).ToList();
        var likeCounts = await _commentRepository.GetLikeCountsByCommentIdsAsync(commentIds, cancellationToken);
        var likedIds = currentUserId.HasValue
            ? await _commentRepository.GetLikedCommentIdsForUserAsync(
                currentUserId.Value,
                commentIds,
                cancellationToken)
            : new HashSet<Guid>();

        return comments
            .Select(comment => MapComment(
                comment,
                likeCounts.GetValueOrDefault(comment.Id),
                likedIds.Contains(comment.Id)))
            .ToList();
    }

    public async Task<(CommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid videoId,
        Guid userId,
        string text,
        Guid? parentCommentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _commentRepository.VideoExistsAsync(videoId, cancellationToken))
        {
            return (null, "Видео не найдено.", StatusCodes.Status404NotFound);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            return (null, "Сначала задайте юзернейм в личном кабинете.", StatusCodes.Status400BadRequest);
        }

        if (parentCommentId.HasValue)
        {
            var parent = await _commentRepository.GetByIdAsync(parentCommentId.Value, cancellationToken);
            if (parent == null)
            {
                return (null, "Родительский комментарий не найден.", StatusCodes.Status404NotFound);
            }

            if (parent.VideoId != videoId)
            {
                return (null, "Нельзя ответить на комментарий из другого видео.", StatusCodes.Status400BadRequest);
            }
        }

        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, "Текст комментария не может быть пустым.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.Length > MaxCommentLength)
        {
            return (null, $"Комментарий не может быть длиннее {MaxCommentLength} символов.", StatusCodes.Status400BadRequest);
        }

        var comment = new CommentEntity
        {
            Id = Guid.NewGuid(),
            VideoId = videoId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            Text = trimmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _commentRepository.AddAsync(comment, cancellationToken);
        comment.User = user;

        return (MapComment(comment, 0, false), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteCommentAsync(
        Guid commentId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(commentId, cancellationToken);
        if (comment == null)
        {
            return (false, "Комментарий не найден.", StatusCodes.Status404NotFound);
        }

        if (!isAdmin && comment.UserId != userId)
        {
            return (false, "Недостаточно прав для удаления комментария.", StatusCodes.Status403Forbidden);
        }

        await _commentRepository.DeleteAsync(comment, cancellationToken);
        return (true, null, StatusCodes.Status200OK);
    }

    public async Task<(ToggleCommentLikeResponse? Response, string? Error, int StatusCode)> ToggleLikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(commentId, cancellationToken);
        if (comment == null)
        {
            return (null, "Комментарий не найден.", StatusCodes.Status404NotFound);
        }

        var existingLike = await _commentRepository.GetLikeAsync(userId, commentId, cancellationToken);
        if (existingLike != null)
        {
            await _commentRepository.RemoveLikeAsync(existingLike, cancellationToken);
        }
        else
        {
            await _commentRepository.AddLikeAsync(
                new CommentLikeEntity
                {
                    UserId = userId,
                    CommentId = commentId,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }

        var likeCount = await _commentRepository.GetLikeCountAsync(commentId, cancellationToken);
        return (
            new ToggleCommentLikeResponse
            {
                IsLiked = existingLike == null,
                LikeCount = likeCount
            },
            null,
            StatusCodes.Status200OK);
    }

    private static CommentResponse MapComment(CommentEntity comment, int likeCount, bool isLiked) =>
        new()
        {
            Id = comment.Id,
            VideoId = comment.VideoId,
            UserId = comment.UserId,
            Username = comment.User?.Username ?? string.Empty,
            Text = comment.Text,
            CreatedAt = comment.CreatedAtUtc,
            ParentCommentId = comment.ParentCommentId,
            LikeCount = likeCount,
            IsLikedByCurrentUser = isLiked
        };
}
