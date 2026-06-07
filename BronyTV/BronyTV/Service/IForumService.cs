using BronyTV.Contract;

namespace BronyTV.Service;

public interface IForumService
{
    Task<IReadOnlyList<ForumThreadResponse>> GetThreadsAsync(CancellationToken cancellationToken = default);

    Task<(ForumThreadResponse? Response, string? Error, int StatusCode)> CreateThreadAsync(
        Guid authorId,
        string title,
        string? description,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumPostResponse>> GetPostsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

    Task<(ForumPostResponse? Response, string? Error, int StatusCode)> CreatePostAsync(
        Guid threadId,
        Guid authorId,
        string content,
        CancellationToken cancellationToken = default);
}
