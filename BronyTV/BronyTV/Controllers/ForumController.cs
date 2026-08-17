using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/forum")]
public class ForumController : ControllerBase
{
        private readonly IForumService _forumService;
    private readonly IUserActivityService _userActivityService;

    public ForumController(
        IForumService forumService,
        IUserActivityService userActivityService)
    {
        _forumService = forumService;
        _userActivityService = userActivityService;
    }

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads(CancellationToken cancellationToken)
    {
        var threads = await _forumService.GetThreadsAsync(cancellationToken);
        return Ok(threads);
    }

    [Authorize(Roles = "User")]
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(
        [FromBody] CreateForumThreadRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _forumService.CreateThreadAsync(
            userId,
            request.Title,
            request.Description,
            request.Images,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpDelete("threads/{threadId:guid}")]
    public async Task<IActionResult> DeleteThread(Guid threadId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var currentUserRole = User.IsInRole("Owner") ? "owner" : User.IsInRole("Admin") ? "admin" : "user";

        var (success, error, statusCode) = await _forumService.DeleteThreadAsync(
            threadId,
            userId,
            currentUserRole,
            cancellationToken);

        if (!success)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return NoContent();
    }

        [HttpGet("threads/{threadId:guid}/posts")]
    public async Task<IActionResult> GetPosts(Guid threadId, CancellationToken cancellationToken)
    {
        var posts = await _forumService.GetPostsAsync(threadId, cancellationToken);

        // Логируем факт просмотра темы только для залогиненных пользователей (гости не логируются).
        if (TryGetUserId(out var viewerId))
        {
            var threadTitle = await _forumService.GetThreadTitleByIdAsync(threadId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(threadTitle))
            {
                await _userActivityService.RecordAsync(
                    viewerId,
                    "forum_view",
                    threadTitle,
                    CancellationToken.None);
            }
        }

        return Ok(posts);
    }

    [Authorize(Roles = "User")]
    [HttpPost("threads/{threadId:guid}/posts")]
    public async Task<IActionResult> CreatePost(
        Guid threadId,
        [FromBody] CreateForumPostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

                var (response, error, statusCode) = await _forumService.CreatePostAsync(
            threadId,
            userId,
            request.Content,
            request.Images,
            request.ReplyToPostId,
            cancellationToken);

                if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        // Логируем факт написания поста (тема, НЕ текст самого поста).
        var threadTitle = await _forumService.GetThreadTitleByIdAsync(threadId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(threadTitle))
        {
            await _userActivityService.RecordAsync(
                userId,
                "forum_post",
                threadTitle,
                CancellationToken.None);
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpPost("posts/{postId:guid}/like")]
    public async Task<IActionResult> ToggleLike(Guid postId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _forumService.ToggleLikeAsync(
            postId,
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var currentUserRole = User.IsInRole("Owner") ? "owner" : User.IsInRole("Admin") ? "admin" : "user";

        var (success, error, statusCode) = await _forumService.DeletePostAsync(
            postId,
            userId,
            currentUserRole,
            cancellationToken);

        if (!success)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
