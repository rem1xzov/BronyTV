using System;

namespace BronyTV.DbContext.Entity;

/// <summary>
/// Состояние стрика (ежедневной активности) пользователя. Один на пользователя.
/// </summary>
public class UserStreakEntity
{
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Текущая длина стрика в днях.</summary>
    public int CurrentStreak { get; set; }

    /// <summary>Рекордная длина стрика за всё время.</summary>
    public int LongestStreak { get; set; }

    /// <summary>Последний день, за который зачтён стрик (календарный день).</summary>
    public DateOnly LastActiveDate { get; set; }

    /// <summary>Сколько заморозок доступно в текущем месяце (максимум 3, не накапливаются).</summary>
    public int FreezesAvailable { get; set; } = 3;

    /// <summary>Сколько заморозок израсходовано в текущем месяце (для отображения).</summary>
    public int FreezesUsedThisMonth { get; set; }

    /// <summary>
    /// Месяц, к которому относятся <see cref="FreezesAvailable"/> и
    /// <see cref="FreezesUsedThisMonth"/> в формате ГГГГММ (например 202609).
    /// Нужен, чтобы лениво сбрасывать заморозки в начале календарного месяца.
    /// </summary>
    public int FreezesMonth { get; set; }

    /// <summary>Дата, на которую пользователь заранее поставил заморозку (nullable).</summary>
    public DateOnly? PendingFreezeDate { get; set; }
}
