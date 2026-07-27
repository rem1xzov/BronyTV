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

    public ForumController(IForumService forumService)
    {
        _forumService = forumService;
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

    [HttpGet("threads/{threadId:guid}/posts")]
    public async Task<IActionResult> GetPosts(Guid threadId, CancellationToken cancellationToken)
    {
        var posts = await _forumService.GetPostsAsync(threadId, cancellationToken);
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
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
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
