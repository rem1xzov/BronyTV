using System;
using System.Collections.Generic;

namespace BronyTV.Contract;

/// <summary>Награда из истории стриков пользователя (для личного кабинета).</summary>
public class StreakRewardItemResponse
{
    public int Milestone { get; set; }
    public DateTime ClaimedAtUtc { get; set; }
    public string RewardDescription { get; set; } = string.Empty;
}

/// <summary>Одна веха в роадмап-плашке с состоянием achieved/next/locked.</summary>
public class StreakMilestoneResponse
{
    public int Milestone { get; set; }
    public string RewardDescription { get; set; } = string.Empty;

    /// <summary>achieved | next | locked.</summary>
    public string State { get; set; } = "locked";

    /// <summary>Веха, наградой которой является колесо фортуны (50/100).</summary>
    public bool IsWheel { get; set; }
}

/// <summary>Непоказанная (IsRewardSeen=false) награда — для одноразовой модалки.</summary>
public class StreakPendingRewardResponse
{
    public int Milestone { get; set; }
    public string RewardDescription { get; set; } = string.Empty;
    public bool IsWheel { get; set; }
}

/// <summary>Полный статус стрика текущего пользователя.</summary>
public class StreakStatusResponse
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    /// <summary>Засчитан ли сегодняшний день (огонёк «горит»).</summary>
    public bool IsStreakCreditedToday { get; set; }

    /// <summary>Минуты активного просмотра/общения за сегодня.</summary>
    public decimal ActiveMinutesToday { get; set; }

    /// <summary>Число засчитанных комментариев за сегодня (максимум 3).</summary>
    public int QualifyingCommentsToday { get; set; }

    /// <summary>Суммарный прогресс за сегодня в минутах (просмотр + комменты).</summary>
    public decimal TotalMinutesToday { get; set; }

    /// <summary>Порог зачёта дня, в минутах.</summary>
    public decimal ThresholdMinutes { get; set; }

    /// <summary>Минимальная длина комментария в словах для зачёта.</summary>
    public int MinCommentWordCount { get; set; }

    /// <summary>Максимум засчитываемых комментариев в день (каждый даёт 3 минуты).</summary>
    public int MaxQualifyingCommentsPerDay { get; set; }

    /// <summary>Следующая веха с наградой (null, если все пройдены).</summary>
    public int? NextMilestone { get; set; }

    /// <summary>Сколько дней осталось до следующей награды.</summary>
    public int? DaysToNextMilestone { get; set; }

    /// <summary>Описание награды за следующую веху.</summary>
    public string? NextMilestoneRewardDescription { get; set; }

    public int FreezesAvailable { get; set; }
    public int FreezesUsedThisMonth { get; set; }

    /// <summary>Дата, на которую поставлена заморозка (null — не поставлена).</summary>
    public DateOnly? PendingFreezeDate { get; set; }

    /// <summary>Можно ли поставить заморозку сейчас (есть свободные и ещё не поставлена).</summary>
    public bool CanFreeze { get; set; }

    public List<StreakRewardItemResponse> Rewards { get; set; } = new();

    /// <summary>Все вехи с состоянием (achieved/next/locked) — для роадмап-плашки.</summary>
    public List<StreakMilestoneResponse> Milestones { get; set; } = new();

    /// <summary>Непоказанная награда (для одноразовой модалки поздравления); null — нет.</summary>
    public StreakPendingRewardResponse? PendingReward { get; set; }
}

/// <summary>Результат записи активности (просмотр/бот/комментарий).</summary>
public class StreakActivityResultResponse
{
    /// <summary>Правда, если именно этим действием день был засчитан в стрик.</summary>
    public bool StreakCreditedNow { get; set; }

    /// <summary>Описание награды, если достигнута новая веха (иначе null).</summary>
    public string? NewRewardDescription { get; set; }

    /// <summary>Веха, если достигнута новая веха (иначе null).</summary>
    public int? NewRewardMilestone { get; set; }

    /// <summary>True, если новая награда — колесо фортуны (нужно показать модалку колеса).</summary>
    public bool IsWheelReward { get; set; }

    public StreakStatusResponse Status { get; set; } = new();
}

/// <summary>Результат установки заморозки.</summary>
public class StreakFreezeResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FreezesAvailable { get; set; }
    public DateOnly? PendingFreezeDate { get; set; }
}

/// <summary>Запись в таблице лидеров.</summary>
public class StreakLeaderboardEntryResponse
{
    public int Rank { get; set; }
    public string Username { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    /// <summary>Засчитан ли у пользователя сегодняшний день.</summary>
    public bool IsStreakCreditedToday { get; set; }
}

/// <summary>Таблица лидеров стриков.</summary>
public class StreakLeaderboardResponse
{
    public List<StreakLeaderboardEntryResponse> Entries { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>Запрос на запись минут активности (видео/бот).</summary>
public class StreakRecordMinutesRequest
{
    /// <summary>Секунды активного времени (просмотр видео или диалог с ботом).</summary>
    public double Seconds { get; set; }
}

/// <summary>Результат вращения колеса фортуны.</summary>
public class FortuneWheelSpinResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>Тип приза: vpn30 / premium1y / vpn1y / nft.</summary>
    public string PrizeType { get; set; } = string.Empty;

    /// <summary>Человекочитаемое описание приза.</summary>
    public string PrizeDescription { get; set; } = string.Empty;

    /// <summary>Индекс сектора колеса (для анимации на клиенте).</summary>
    public int PrizeIndex { get; set; }

    /// <summary>True, если выдачу нужно завершить вручную (NFT-подарок).</summary>
    public bool RequiresManualAction { get; set; }

    /// <summary>Сообщение для пользователя.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Ссылка на аккаунт/чат поддержки в Telegram (для ручной выдачи).</summary>
    public string? SupportTelegramUrl { get; set; }
}
