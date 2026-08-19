using System;

namespace BronyTV.Contract;

/// <summary>Ответ для фронтенда: статус VPN-подписки пользователя.</summary>
public class VpnStatusResponse
{
    public bool Enabled { get; set; }
    public bool IsActive { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int? DaysLeft { get; set; }
    public bool IsTrialUsed { get; set; }
    public string? VlessLink { get; set; }
    public string? PanelClientUrl { get; set; }
    public string? ClientDownloadUrl { get; set; }
    public string? ReferralCode { get; set; }
    public int ReferralBonusDays { get; set; }
}

/// <summary>Ответ о результате активации промо-кода.</summary>
public class VpnPromoActivateResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Запрос на активацию промо-кода.</summary>
public class VpnPromoActivateRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>Ответ об успешном старте trial-подписки.</summary>
public class VpnTrialStartResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Запрос на создание подписки вручную (админка).</summary>
public class VpnAdminCreateSubscriptionRequest
{
    public Guid UserId { get; set; }
    public string? PlanName { get; set; }
    public int Days { get; set; }
    public bool IsTrial { get; set; }
    public string? ClientUuid { get; set; }
    public string? Note { get; set; }
}

/// <summary>Описание подписки в списке пользователей админки.</summary>
public class VpnAdminSubscriptionItem
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string Kind { get; set; } = "trial";
    public string PlanName { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}

/// <summary>Список подписок (для админки).</summary>
public class VpnAdminSubscriptionListResponse
{
    public System.Collections.Generic.List<VpnAdminSubscriptionItem> Items { get; set; } = new();
    public int Total { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>Промо-ключ в списке админки.</summary>
public class VpnAdminPromoKeyItem
{
    public string Code { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? UsedByUsername { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

/// <summary>Список промо-ключей (для админки).</summary>
public class VpnAdminPromoKeyListResponse
{
    public System.Collections.Generic.List<VpnAdminPromoKeyItem> Items { get; set; } = new();
    public int Total { get; set; }
    public int Unused { get; set; }
}

/// <summary>Реферальное начисление в списке админки.</summary>
public class VpnAdminReferralRewardItem
{
    public Guid ReferrerId { get; set; }
    public string? ReferrerUsername { get; set; }
    public Guid ReferralUserId { get; set; }
    public string? ReferralUsername { get; set; }
    public int BonusDays { get; set; }
    public string Reason { get; set; } = "register";
    public bool IsRedeemed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Список реферальных начислений (для админки).</summary>
public class VpnAdminReferralRewardListResponse
{
    public System.Collections.Generic.List<VpnAdminReferralRewardItem> Items { get; set; } = new();
    public int Total { get; set; }
}
