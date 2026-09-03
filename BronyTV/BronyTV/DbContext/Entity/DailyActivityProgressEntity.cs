using System;

namespace BronyTV.DbContext.Entity;

/// <summary>
/// Прогресс активности пользователя за конкретные календарные сутки. Позволяет не
/// пересчитывать сумму минут с нуля при каждом действии. Одна запись на (пользователь, дата).
/// </summary>
public class DailyActivityProgressEntity
{
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Суммарное время просмотра видео + время общения с ботом, в минутах.</summary>
    public decimal ActiveMinutes { get; set; }

    /// <summary>
    /// Количество комментариев на форуме, прошедших проверку длины (≥5 слов после trim).
    /// Каждый засчитывается как 3 минуты, максимум 3 комментария (9 минут) в день.
    /// </summary>
    public int QualifyingCommentsCount { get; set; }

    /// <summary>Засчитан ли день в стрик (порог 10 минут уже достигнут).</summary>
    public bool IsStreakCredited { get; set; }
}
