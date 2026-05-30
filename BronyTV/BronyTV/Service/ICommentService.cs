using BronyTV.Contract;

namespace BronyTV.Service;

public interface ICommentService
{
    Task<IReadOnlyList<CommentResponse>> GetCommentsForVideoAsync(
        Guid videoId,
        CancellationToken cancellationToken = default);

    Task<(CommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid videoId,
        Guid userId,
        string text,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeleteCommentAsync(
        Guid commentId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
