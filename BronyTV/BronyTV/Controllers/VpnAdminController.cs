using System.Security.Claims;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Административные эндпоинты BronyVPN: генерация промо-ключей,
/// списки подписок и реферальных начислений (выдача бонусных дней).
/// </summary>
[ApiController]
[Route("api/admin/vpn")]
[Authorize(Roles = "Admin")]
public class VpnAdminController : ControllerBase
{
    private readonly IVpnAdminService _vpnAdminService;

    public VpnAdminController(IVpnAdminService vpnAdminService)
    {
        _vpnAdminService = vpnAdminService;
    }

    /// <summary>Генерирует один промо-ключ для выдачи покупателю.</summary>
    [HttpPost("promo-keys/generate")]
    public async Task<IActionResult> GeneratePromoKey(CancellationToken cancellationToken)
    {
        var code = await _vpnAdminService.GeneratePromoKeyAsync(cancellationToken);
        return Ok(new { Code = code });
    }

    /// <summary>Список промо-ключей.</summary>
    [HttpGet("promo-keys/list")]
    public async Task<IActionResult> ListPromoKeys(CancellationToken cancellationToken)
    {
        var result = await _vpnAdminService.ListPromoKeysAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Список подписок VPN (для выдачи/контроля).</summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> ListSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _vpnAdminService.ListSubscriptionsAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Список реферальных начислений (для выдачи бонусных дней).</summary>
    [HttpGet("referral-rewards")]
    public async Task<IActionResult> ListReferralRewards(CancellationToken cancellationToken)
    {
        var result = await _vpnAdminService.ListReferralRewardsAsync(cancellationToken);
        return Ok(result);
    }
}
