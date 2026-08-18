using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoriteController : ControllerBase
{
    private readonly IUserFavoriteService _favoriteService;

    public FavoriteController(IUserFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyFavorites(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var favorites = await _favoriteService.GetByUserAsync(userId, cancellationToken);
        return Ok(favorites);
    }

    [HttpGet("{videoId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid videoId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var isFavorite = await _favoriteService.IsFavoriteAsync(userId, videoId, cancellationToken);
        return Ok(new FavoriteStatusResponse { IsFavorite = isFavorite });
    }

    [HttpPost("{videoId:guid}")]
    public async Task<IActionResult> Add(Guid videoId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _favoriteService.AddAsync(userId, videoId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var isFavorite = await _favoriteService.IsFavoriteAsync(userId, videoId, cancellationToken);
        return Ok(new FavoriteStatusResponse { IsFavorite = isFavorite });
    }

    [HttpDelete("{videoId:guid}")]
    public async Task<IActionResult> Remove(Guid videoId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _favoriteService.RemoveAsync(userId, videoId, cancellationToken);

        var isFavorite = await _favoriteService.IsFavoriteAsync(userId, videoId, cancellationToken);
        return Ok(new FavoriteStatusResponse { IsFavorite = isFavorite });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
