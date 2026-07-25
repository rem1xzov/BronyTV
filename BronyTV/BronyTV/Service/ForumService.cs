using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;

namespace BronyTV.Service;

public class ForumService : IForumService
{
    private const int MaxTitleLength = 150;
    private const int MaxContentLength = 4000;
    private readonly IForumRepository _forumRepository;
    private readonly IUserRepository _userRepository;

    public ForumService(IForumRepository forumRepository, IUserRepository userRepository)
    {
        _forumRepository = forumRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<ForumThreadResponse>> GetThreadsAsync(
        CancellationToken cancellationToken = default)
    {
        var threads = await _forumRepository.GetThreadsAsync(cancellationToken);
        return threads.Select(MapThread).ToList();
    }

    public async Task<(ForumThreadResponse? Response, string? Error, int StatusCode)> CreateThreadAsync(
        Guid authorId,
        string title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var author = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (author == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(author.Username))
        {
            return (null, "Сначала задайте юзернейм в личном кабинете.", StatusCodes.Status400BadRequest);
        }

        var trimmedTitle = title.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
        {
            return (null, "Укажите заголовок темы.", StatusCodes.Status400BadRequest);
        }

        if (trimmedTitle.Length > MaxTitleLength)
        {
            return (null, $"Заголовок не может быть длиннее {MaxTitleLength} символов.", StatusCodes.Status400BadRequest);
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > MaxContentLength)
        {
            return (null, $"Описание не может быть длиннее {MaxContentLength} символов.", StatusCodes.Status400BadRequest);
        }

        var thread = new ForumThreadEntity
        {
            Id = Guid.NewGuid(),
            Title = trimmedTitle,
            Description = trimmedDescription,
            AuthorId = authorId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _forumRepository.AddThreadAsync(thread, cancellationToken);
        thread.Author = author;
        return (MapThread(thread), null, StatusCodes.Status200OK);
    }

    public async Task<IReadOnlyList<ForumPostResponse>> GetPostsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        if (await _forumRepository.GetThreadByIdAsync(threadId, cancellationToken) == null)
        {
            return Array.Empty<ForumPostResponse>();
        }

        var posts = await _forumRepository.GetPostsByThreadIdAsync(threadId, cancellationToken);
        return posts.Select(MapPost).ToList();
    }

    public async Task<(ForumPostResponse? Response, string? Error, int StatusCode)> CreatePostAsync(
        Guid threadId,
        Guid authorId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (await _forumRepository.GetThreadByIdAsync(threadId, cancellationToken) == null)
        {
            return (null, "Тема не найдена.", StatusCodes.Status404NotFound);
        }

        var author = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (author == null)
        {
            return (null, "Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(author.Username))
        {
            return (null, "Сначала задайте юзернейм в личном кабинете.", StatusCodes.Status400BadRequest);
        }

        var trimmed = content.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, "Сообщение не может быть пустым.", StatusCodes.Status400BadRequest);
        }

        if (trimmed.Length > MaxContentLength)
        {
            return (null, $"Сообщение не может быть длиннее {MaxContentLength} символов.", StatusCodes.Status400BadRequest);
        }

        var post = new ForumPostEntity
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            Content = trimmed,
            AuthorId = authorId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _forumRepository.AddPostAsync(post, cancellationToken);
        post.Author = author;
        return (MapPost(post), null, StatusCodes.Status200OK);
    }

    private static ForumThreadResponse MapThread(ForumThreadEntity thread) =>
        new()
        {
            Id = thread.Id,
            Title = thread.Title,
            Description = thread.Description,
            CreatedAt = thread.CreatedAtUtc,
            AuthorId = thread.AuthorId,
            AuthorUsername = thread.Author?.Username ?? string.Empty,
            PostCount = thread.Posts?.Count ?? 0
        };

    private static ForumPostResponse MapPost(ForumPostEntity post) =>
        new()
        {
            Id = post.Id,
            ThreadId = post.ThreadId,
            Content = post.Content,
            CreatedAt = post.CreatedAtUtc,
            AuthorId = post.AuthorId,
            AuthorUsername = post.Author?.Username ?? string.Empty
        };
}
