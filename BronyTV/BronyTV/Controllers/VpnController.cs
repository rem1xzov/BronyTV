using System.Security.Claims;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BronyTV.Contract;

namespace BronyTV.Controllers;

/// <summary>
/// Пользовательские эндпоинты BronyVPN (статус, trial, промо-код).
/// Доступ через cookie-авторизацию залогиненного пользователя.
/// </summary>
[ApiController]
[Route("api/vpn")]
[Authorize(Roles = "User")]
public class VpnController : ControllerBase
{
    private readonly IVpnService _vpnService;

    public VpnController(IVpnService vpnService)
    {
        _vpnService = vpnService;
    }

    /// <summary>Статус VPN-подписки текущего пользователя.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _vpnService.GetStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    /// <summary>Активация trial-подписки.</summary>
    [HttpPost("trial")]
    public async Task<IActionResult> StartTrial(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (success, error, response, serverError) = await _vpnService.StartTrialAsync(userId, cancellationToken);
        if (!success)
        {
            return serverError
                ? StatusCode(StatusCodes.Status502BadGateway, new { message = error ?? "Ошибка VPN-провайдера." })
                : BadRequest(new { message = error ?? "Не удалось активировать trial." });
        }

        return Ok(response);
    }

    /// <summary>Активация промо-кода.</summary>
    [HttpPost("promo")]
    public async Task<IActionResult> ActivatePromo([FromBody] VpnPromoActivateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (success, error, response, serverError) = await _vpnService.ActivatePromoCodeAsync(
            userId,
            request?.Code ?? string.Empty,
            cancellationToken);
        if (!success)
        {
            return serverError
                ? StatusCode(StatusCodes.Status502BadGateway, new { message = error ?? "Ошибка VPN-провайдера." })
                : BadRequest(new { message = error ?? "Не удалось активировать промо-код." });
        }

        return Ok(response);
    }

    /// <summary>Отключение собственной VPN-подписки.</summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _vpnService.RevokeAsync(userId, cancellationToken);
        return Ok();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
