namespace BronyTV.DbContext.Entity;

/// <summary>
/// Подписка пользователя на BronyVPN. Может быть trial-подпиской
/// или оплаченным пакетом, активированным по промо-коду.
/// </summary>
public class VpnSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Тип подписки: trial / promo / manual.</summary>
    public string Kind { get; set; } = "trial";

    /// <summary>Имя пакета (например «BronyVPN 1 месяц»).</summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>Дата начала.</summary>
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Дата окончания (null = бессрочно).</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Если issued — UUID клиента из панели 3X-UI.</summary>
    public string? ClientUuid { get; set; }

    /// <summary>Комментарий администратора (необязательно).</summary>
    public string? Note { get; set; }

    /// <summary>Неактивна ли подписка (админ может отключить вручную).</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Ссылка на 3X-UI-имя тарифа (например «1-month»).</summary>
    public string? PanelPlanNameId { get; set; }
}
