using BronyTV.Contract;

namespace BronyTV.Service;

public interface ICommentService
{
    Task<IReadOnlyList<CommentResponse>> GetCommentsForVideoAsync(
        Guid videoId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<(CommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid videoId,
        Guid userId,
        string text,
        Guid? parentCommentId = null,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeleteCommentAsync(
        Guid commentId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<(ToggleCommentLikeResponse? Response, string? Error, int StatusCode)> ToggleLikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
