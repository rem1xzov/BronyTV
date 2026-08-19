namespace BronyTV.DbContext.Entity;

/// <summary>
/// Начисление бонусных дней BronyVPN за приглашённого реферала.
/// Начисляется при создании (регистрации нового пользователя по реферальной ссылке)
/// и при подтверждении email приглашённого.
/// </summary>
public class ReferralRewardEntity
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, который получил бонус (реферер).</summary>
    public Guid ReferrerId { get; set; }
    public UserEntity? Referrer { get; set; }

    /// <summary>Пользователь, который зарегистрировался по реферальной ссылке.</summary>
    public Guid ReferralUserId { get; set; }
    public UserEntity? ReferralUser { get; set; }

    /// <summary>Сколько дней начислено.</summary>
    public int BonusDays { get; set; }

    /// <summary>Основание для начисления (register / email_confirmed).</summary>
    public string Reason { get; set; } = "register";

    /// <summary>Создание записи.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Израсходован ли бонус (пришёл ли через промо-режим/активен). — зарезервировано.</summary>
    public bool IsRedeemed { get; set; }
}
