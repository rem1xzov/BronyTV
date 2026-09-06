using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly INewsPostRepository _newsRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserActivityService _userActivityService;
    private readonly INewsCommentService _newsCommentService;
    private readonly IStreakService _streakService;

    public NewsController(
        INewsPostRepository newsRepository,
        IUserRepository userRepository,
        IUserActivityService userActivityService,
        INewsCommentService newsCommentService,
        IStreakService streakService)
    {
        _newsRepository = newsRepository;
        _userRepository = userRepository;
        _userActivityService = userActivityService;
        _newsCommentService = newsCommentService;
        _streakService = streakService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var news = await _newsRepository.GetAllAsync(cancellationToken);
        return Ok(news.Select(MapToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news == null)
        {
            return NotFound(new { message = "Новость не найдена." });
        }

        // Логируем факт просмотра новости только для залогиненных пользователей.
        if (TryGetUserId(out var viewerId) && !string.IsNullOrWhiteSpace(news.Title))
        {
            await _userActivityService.RecordAsync(
                viewerId,
                "news_view",
                news.Title,
                CancellationToken.None);
        }

        return Ok(MapToResponse(news));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNewsPostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        var news = new NewsPost
        {
            Id = Guid.NewGuid(),
            Title = request.Title?.Trim(),
            Content = request.Content?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            AuthorUsername = user.Username ?? user.Email ?? "unknown",
            CreatedAt = DateTime.UtcNow
        };

        await _newsRepository.AddAsync(news, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = news.Id }, MapToResponse(news));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news == null)
        {
            return NotFound(new { message = "Новость не найдена." });
        }

        await _newsRepository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
    {
        var comments = await _newsCommentService.GetCommentsAsync(id, cancellationToken);
        return Ok(comments);
    }

    [Authorize(Roles = "User")]
    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> CreateComment(
        Guid id,
        [FromBody] CreateNewsCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _newsCommentService.CreateCommentAsync(
            id,
            userId,
            request.Content,
            request.Images,
            request.ReplyToCommentId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        // Логируем факт комментария (название новости, НЕ текст комментария).
        var news = await _newsRepository.GetByIdAsync(id, cancellationToken);
        if (news != null && !string.IsNullOrWhiteSpace(news.Title))
        {
            await _userActivityService.RecordAsync(
                userId,
                "news_comment",
                news.Title,
                CancellationToken.None);
        }

        // Учитываем комментарий в прогрессе стрика (≥5 слов → +3 минуты, максимум 3 в день).
        await _streakService.RecordForumCommentAsync(
            userId,
            request.Content,
            CancellationToken.None);

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpPost("comments/{commentId:guid}/like")]
    public async Task<IActionResult> ToggleCommentLike(Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (response, error, statusCode) = await _newsCommentService.ToggleLikeAsync(
            commentId,
            userId,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var currentUserRole = User.IsInRole("Owner") ? "owner" : User.IsInRole("Admin") ? "admin" : "user";

        var (success, error, statusCode) = await _newsCommentService.DeleteCommentAsync(
            commentId,
            userId,
            currentUserRole,
            cancellationToken);

        if (!success)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return NoContent();
    }

    private static NewsPostResponse MapToResponse(NewsPost news) =>
        new()
        {
            Id = news.Id,
            Title = news.Title,
            Content = news.Content,
            ImageUrl = news.ImageUrl,
            AuthorUsername = news.AuthorUsername,
            CreatedAt = news.CreatedAt
        };

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}