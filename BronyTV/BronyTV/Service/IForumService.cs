using BronyTV.Contract;

namespace BronyTV.Service;

public interface IForumService
{
    Task<IReadOnlyList<ForumThreadResponse>> GetThreadsAsync(CancellationToken cancellationToken = default);

    Task<(ForumThreadResponse? Response, string? Error, int StatusCode)> CreateThreadAsync(
        Guid authorId,
        string title,
        string? description,
        List<string>? images,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeleteThreadAsync(
        Guid threadId,
        Guid userId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumPostResponse>> GetPostsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

        Task<(ForumPostResponse? Response, string? Error, int StatusCode)> CreatePostAsync(
        Guid threadId,
        Guid authorId,
        string content,
        List<string>? images,
        Guid? replyToPostId,
        CancellationToken cancellationToken = default);

    Task<(ForumPostResponse? Response, string? Error, int StatusCode)> ToggleLikeAsync(
        Guid postId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, int StatusCode)> DeletePostAsync(
        Guid postId,
        Guid userId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
