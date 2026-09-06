using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;

namespace BronyTV.Service;

public interface INewsCommentService
{
    Task<IReadOnlyList<NewsCommentResponse>> GetCommentsAsync(
        Guid newsId,
        CancellationToken cancellationToken = default);

    Task<(NewsCommentResponse? Response, string? Error, int StatusCode)> CreateCommentAsync(
        Guid newsId,
        Guid authorId,
        string content,
        List<string>? images,
        Guid? replyToCommentId,
        CancellationToken cancellationToken = default);

    Task<(NewsCommentResponse? Response, string? Error, int StatusCode)> ToggleLikeAsync(
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeleteCommentAsync(
        Guid commentId,
        Guid userId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
