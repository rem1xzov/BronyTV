using System;
using System.Linq;
using BronyTV.DbContext.Entity;

namespace BronyTV.Service;

/// <summary>
/// Чистая (без I/O) логика стриков. Вынесена отдельно, чтобы её можно было
/// юнит-тестировать без базы данных. Все методы принимают «сегодня» явно.
/// </summary>
public static class StreakLogic
{
    public const decimal ThresholdMinutes = 10m;

    public const int CommentMinutes = 3;
    public const int MaxQualifyingComments = 3;

    /// <summary>Минимальная длина комментария в словах для зачёта (после trim).</summary>
    public const int MinCommentWordCount = 5;

    public const int FreezesPerMonth = 3;

    public static readonly int[] Milestones = { 3, 7, 14, 30, 50, 100 };

    public const string WheelRewardDescription = "Колесо фортуны";

    /// <summary>Число слов в тексте (split по пробельным символам).</summary>
    public static int CountWords(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return content
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    public static bool IsQualifyingComment(string? content)
        => CountWords(content) >= MinCommentWordCount;

    /// <summary>Минуты, начисленные за засчитанные комментарии (максимум 9 в день).</summary>
    public static decimal CommentMinutesValue(int qualifyingCommentsCount)
        => Math.Min(qualifyingCommentsCount, MaxQualifyingComments) * CommentMinutes;

    /// <summary>Суммарный прогресс за день в минутах (просмотр/бот + комментарии).</summary>
    public static decimal TotalMinutes(decimal activeMinutes, int qualifyingCommentsCount)
        => activeMinutes + CommentMinutesValue(qualifyingCommentsCount);

    public static bool HasReachedThreshold(decimal activeMinutes, int qualifyingCommentsCount)
        => TotalMinutes(activeMinutes, qualifyingCommentsCount) >= ThresholdMinutes;

    public static int? GetNextMilestone(int currentStreak)
    {
        foreach (var milestone in Milestones)
        {
            if (milestone > currentStreak)
            {
                return milestone;
            }
        }

        return null;
    }

    public static string GetRewardDescription(int milestone) => milestone switch
    {
        3 => "Бейдж на профиле + 1 день VPN",
        7 => "5 дней VPN + 7 дней премиум",
        14 => "7 дней VPN + 10 дней премиум",
        30 => "14 дней VPN + 14 дней премиум",
        50 => WheelRewardDescription,
        100 => WheelRewardDescription,
        _ => "Награда за стрик"
    };

    /// <summary>Сколько дней VPN начисляется за веху (0 для колеса).</summary>
    public static int GetMilestoneVpnDays(int milestone) => milestone switch
    {
        3 => 1,
        7 => 5,
        14 => 7,
        30 => 14,
        _ => 0
    };

    /// <summary>Сколько дней премиум начисляется за веху (0 для колеса).</summary>
    public static int GetMilestonePremiumDays(int milestone) => milestone switch
    {
        7 => 7,
        14 => 10,
        30 => 14,
        _ => 0
    };

    public static bool IsWheelMilestone(int milestone) => milestone == 50 || milestone == 100;

    /// <summary>
    /// Ленивая обработка смены суток: обрыв стрика при пропуске дня и расход заморозки.
    /// Также сбрасывает заморозки в начале календарного месяца.
    /// Мутирует <paramref name="streak"/> на месте.
    /// </summary>
    public static void ApplyRollover(UserStreakEntity streak, DateOnly today)
    {
        ResetFreezesIfNewMonth(streak, today);

        // Нет ни одного засчитанного дня — обрывать нечего.
        if (streak.LastActiveDate == default || streak.LastActiveDate >= today)
        {
            return;
        }

        var gapDays = today.DayNumber - streak.LastActiveDate.DayNumber;

        // Вчера был последний активный день — стрик жив.
        if (gapDays == 1)
        {
            ClearStaleFreeze(streak, today);
            return;
        }

        // Пропущен ровно один день, и на него заранее была поставлена заморозка.
        if (gapDays == 2
            && streak.PendingFreezeDate.HasValue
            && streak.PendingFreezeDate.Value == today.AddDays(-1))
        {
            streak.FreezesAvailable = Math.Max(0, streak.FreezesAvailable - 1);
            streak.FreezesUsedThisMonth += 1;
            streak.LastActiveDate = today.AddDays(-1);
            streak.PendingFreezeDate = null;
            return;
        }

        // Пропущен день без заморозки — стрик сбрасывается.
        streak.CurrentStreak = 0;
        streak.PendingFreezeDate = null;
    }

    /// <summary>Зачёт дня: увеличивает стрик и обновляет рекорд.</summary>
    public static void CreditDay(UserStreakEntity streak, DateOnly today)
    {
        streak.CurrentStreak = streak.LastActiveDate == today.AddDays(-1)
            ? streak.CurrentStreak + 1
            : 1;

        streak.LongestStreak = Math.Max(streak.LongestStreak, streak.CurrentStreak);
        streak.LastActiveDate = today;

        ClearStaleFreeze(streak, today);
    }

    public static void ResetFreezesIfNewMonth(UserStreakEntity streak, DateOnly today)
    {
        var month = today.Year * 100 + today.Month;
        if (streak.FreezesMonth != month)
        {
            streak.FreezesMonth = month;
            streak.FreezesAvailable = FreezesPerMonth;
            streak.FreezesUsedThisMonth = 0;
        }
    }

    private static void ClearStaleFreeze(UserStreakEntity streak, DateOnly today)
    {
        if (streak.PendingFreezeDate.HasValue && streak.PendingFreezeDate.Value <= today)
        {
            streak.PendingFreezeDate = null;
        }
    }
}
