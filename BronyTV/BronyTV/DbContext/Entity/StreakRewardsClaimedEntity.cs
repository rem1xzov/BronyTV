using System;

namespace BronyTV.DbContext.Entity;

/// <summary>
/// Факт получения награды за достижение вехи стрика. Каждую веху пользователь
/// получает ровно один раз (составной ключ UserId + Milestone).
/// </summary>
public class StreakRewardsClaimedEntity
{
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Веха (день): 3, 7, 14, 30, 50 или 100.</summary>
    public int Milestone { get; set; }

    public DateTime ClaimedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Человекочитаемое описание выданной награды (для истории в ЛК).</summary>
    public string RewardDescription { get; set; } = string.Empty;

    /// <summary>
    /// Показывалась ли пользователю модалка поздравления за эту веху.
    /// Позволяет показать плашку один раз при следующей загрузке главной страницы,
    /// независимо от того, каким источником (видео/бот/форум) был зачтён день.
    /// </summary>
    public bool IsRewardSeen { get; set; }
}
