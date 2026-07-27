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

public class ForumService : IForumService
{
    private readonly IForumRepository _forumRepository;
    private readonly IUserRepository _userRepository;

    public ForumService(IForumRepository forumRepository, IUserRepository userRepository)
    {
        _forumRepository = forumRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<ForumThreadResponse>> GetThreadsAsync(CancellationToken cancellationToken = default)
    {
        var threads = await _forumRepository.GetThreadsAsync(cancellationToken);
        return threads.Select(ThreadToResponse).ToList();
    }

    public async Task<(ForumThreadResponse? Response, string? Error, int StatusCode)> CreateThreadAsync(
        Guid authorId,
        string title,
        string? description,
        List<string>? images,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (null, "Заголовок не может быть пустым.", 400);
        }

        if (title.Length > 150)
        {
            return (null, "Заголовок слишком длинный.", 400);
        }

        var user = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", 404);
        }

        var thread = new ForumThreadEntity
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description?.Trim(),
            AuthorId = authorId,
            CreatedAtUtc = DateTime.UtcNow,
            Images = images != null ? JsonSerializer.Serialize(images) : null
        };

        await _forumRepository.AddThreadAsync(thread, cancellationToken);

        var response = new ForumThreadResponse
        {
            Id = thread.Id,
            Title = thread.Title,
            Description = thread.Description,
            AuthorUsername = user.Username ?? "unknown",
            CreatedAtUtc = thread.CreatedAtUtc,
            PostCount = 0,
            Images = DeserializeImages(thread.Images)
        };

        return (response, null, 201);
    }

    public async Task<IReadOnlyList<ForumPostResponse>> GetPostsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var posts = await _forumRepository.GetPostsByThreadIdAsync(threadId, cancellationToken);
        return posts.Select(PostToResponse).ToList();
    }

    public async Task<(ForumPostResponse? Response, string? Error, int StatusCode)> CreatePostAsync(
        Guid threadId,
        Guid authorId,
        string content,
        List<string>? images,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (null, "Сообщение не может быть пустым.", 400);
        }

        if (content.Length > 4000)
        {
            return (null, "Сообщение слишком длинное.", 400);
        }

        var thread = await _forumRepository.GetThreadByIdAsync(threadId, cancellationToken);
        if (thread == null)
        {
            return (null, "Тема не найдена.", 404);
        }

        var user = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (user == null)
        {
            return (null, "Пользователь не найден.", 404);
        }

        var post = new ForumPostEntity
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            Content = content.Trim(),
            AuthorId = authorId,
            CreatedAtUtc = DateTime.UtcNow,
            Images = images != null ? JsonSerializer.Serialize(images) : null
        };

        await _forumRepository.AddPostAsync(post, cancellationToken);

        var postResponse = new ForumPostResponse
        {
            Id = post.Id,
            Content = post.Content,
            AuthorUsername = user.Username ?? "unknown",
            CreatedAtUtc = post.CreatedAtUtc,
            Images = DeserializeImages(post.Images)
        };

        return (postResponse, null, 201);
    }

    private static ForumThreadResponse ThreadToResponse(ForumThreadEntity thread) =>
        new ForumThreadResponse
        {
            Id = thread.Id,
            Title = thread.Title ?? string.Empty,
            Description = thread.Description,
            AuthorUsername = thread.Author?.Username ?? "unknown",
            CreatedAtUtc = thread.CreatedAtUtc,
            PostCount = 0,
            Images = DeserializeImages(thread.Images)
        };

    private static ForumPostResponse PostToResponse(ForumPostEntity post) =>
        new ForumPostResponse
        {
            Id = post.Id,
            Content = post.Content ?? string.Empty,
            AuthorUsername = post.Author?.Username ?? "unknown",
            CreatedAtUtc = post.CreatedAtUtc,
            Images = DeserializeImages(post.Images)
        };

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
}
