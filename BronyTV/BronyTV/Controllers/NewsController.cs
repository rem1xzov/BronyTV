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

    public NewsController(
        INewsPostRepository newsRepository,
        IUserRepository userRepository,
        IUserActivityService userActivityService)
    {
        _newsRepository = newsRepository;
        _userRepository = userRepository;
        _userActivityService = userActivityService;
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