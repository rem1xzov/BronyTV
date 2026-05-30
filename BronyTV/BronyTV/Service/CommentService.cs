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
        CancellationToken cancellationToken = default)
    {
        if (!await _commentRepository.VideoExistsAsync(videoId, cancellationToken))
        {
            return Array.Empty<CommentResponse>();
        }

        var comments = await _commentRepository.GetByVideoIdAsync(videoId, cancellationToken);
        return comments.Select(MapComment).ToList();
    }

    public async Task<(CommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid videoId,
        Guid userId,
        string text,
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
            Text = trimmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _commentRepository.AddAsync(comment, cancellationToken);
        comment.User = user;

        return (MapComment(comment), null, StatusCodes.Status200OK);
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

    private static CommentResponse MapComment(CommentEntity comment) =>
        new()
        {
            Id = comment.Id,
            VideoId = comment.VideoId,
            UserId = comment.UserId,
            Username = comment.User?.Username ?? string.Empty,
            Text = comment.Text,
            CreatedAt = comment.CreatedAtUtc
        };
}
