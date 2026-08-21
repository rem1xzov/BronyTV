namespace BronyTV.DbContext.Entity;

/// <summary>
/// Одноразовый промо-код для оплаченной VPN-подписки (выдаётся покупателю вне платформы).
/// </summary>
public class VpnPromoKeyEntity
{
    public string Code { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAtUtc { get; set; }
    public Guid? UsedByUserId { get; set; }
    public UserEntity? UsedByUser { get; set; }
    public Guid? SubscriptionId { get; set; }
    public VpnSubscriptionEntity? Subscription { get; set; }

    /// <summary>UUID клиента из панели, который должен быть привязан к активируемой подписке.</summary>
    public string? ClientUuid { get; set; }

    /// <summary>Длительность подписки в месяцах (1, 3, 6 или 12).</summary>
    public int DurationMonths { get; set; } = 1;
}
