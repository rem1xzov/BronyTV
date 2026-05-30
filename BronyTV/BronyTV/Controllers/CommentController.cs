using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api")]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("videos/{videoId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid videoId, CancellationToken cancellationToken)
    {
        Guid? currentUserId = null;
        if (TryGetUserId(out var userId))
        {
            currentUserId = userId;
        }

        var comments = await _commentService.GetCommentsForVideoAsync(
            videoId,
            currentUserId,
            cancellationToken);
        return Ok(comments);
    }

    [Authorize(Roles = "User")]
    [HttpPost("videos/{videoId:guid}/comments")]
    public async Task<IActionResult> CreateComment(
        Guid videoId,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _commentService.CreateCommentAsync(
            videoId,
            userId,
            request.Text,
            request.ParentCommentId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpPost("comments/{commentId:guid}/like")]
    public async Task<IActionResult> ToggleLike(Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _commentService.ToggleLikeAsync(
            commentId,
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize]
    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole("Admin");
        var (success, error, statusCode) = await _commentService.DeleteCommentAsync(
            commentId,
            userId,
            isAdmin,
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
        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
