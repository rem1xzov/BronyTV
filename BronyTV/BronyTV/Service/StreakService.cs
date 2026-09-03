using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BronyTV.Service;

public class StreakService : IStreakService
{
    private readonly IStreakRepository _repository;
    private readonly IVpnService _vpnService;
    private readonly IPremiumRewardClient _premiumRewardClient;
    private readonly ILogger<StreakService> _logger;
    private readonly string _supportTelegramUrl;

    // Максимум минут, принимаемых за один запрос записи активности — защита от подделки
    // клиентом сколь угодно больших значений (например, одним POST сразу 24 часа).
    private const decimal MaxMinutesPerRequest = 10m;

    public StreakService(
        IStreakRepository repository,
        IVpnService vpnService,
        IPremiumRewardClient premiumRewardClient,
        ILogger<StreakService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _vpnService = vpnService;
        _premiumRewardClient = premiumRewardClient;
        _logger = logger;
        _supportTelegramUrl = configuration["Support:TelegramUrl"]
            ?? "https://t.me/bronytv";
    }

    // «Сегодня» вычисляется по UTC (как и вся остальная активность в проекте).
    // Если бизнес захочет использовать таймзону пользователя/Москвы — меняется только здесь.
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<StreakStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = Today();

        var streak = await _repository.GetStreakAsync(userId, cancellationToken);
        if (streak != null)
        {
            StreakLogic.ApplyRollover(streak, today);
            // Лениво фиксируем смену суток/месяца (обрыв стрика, расход заморозки).
            await _repository.SaveChangesAsync(cancellationToken);
        }

        var progress = await _repository.GetProgressAsync(userId, today, cancellationToken);
        var rewards = await _repository.GetRewardsAsync(userId, cancellationToken);

        return BuildStatus(streak, progress, rewards);
    }

    public async Task<StreakActivityResultResponse> RecordVideoWatchAsync(
        Guid userId,
        double seconds,
        CancellationToken cancellationToken = default)
        => await AddActiveMinutesAsync(userId, seconds, cancellationToken);

    public async Task<StreakActivityResultResponse> RecordBotChatAsync(
        Guid userId,
        double seconds,
        CancellationToken cancellationToken = default)
        => await AddActiveMinutesAsync(userId, seconds, cancellationToken);

    public async Task<StreakActivityResultResponse> RecordForumCommentAsync(
        Guid userId,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Короткий комментарий не засчитывается вообще.
        if (!StreakLogic.IsQualifyingComment(content))
        {
            return new StreakActivityResultResponse
            {
                StreakCreditedNow = false,
                Status = await GetStatusAsync(userId, cancellationToken)
            };
        }

        var today = Today();
        var streak = await _repository.GetOrCreateStreakAsync(userId, cancellationToken);
        StreakLogic.ApplyRollover(streak, today);

        var progress = await _repository.GetOrCreateProgressAsync(userId, today, cancellationToken);
        if (progress.QualifyingCommentsCount < StreakLogic.MaxQualifyingComments)
        {
            progress.QualifyingCommentsCount += 1;
        }

        return await CompleteProgressAsync(userId, streak, progress, today, cancellationToken);
    }

    public async Task<StreakFreezeResponse> SetFreezeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var streak = await _repository.GetOrCreateStreakAsync(userId, cancellationToken);
        StreakLogic.ApplyRollover(streak, today);

        if (streak.PendingFreezeDate.HasValue)
        {
            return new StreakFreezeResponse
            {
                Success = false,
                Error = "Заморозка на следующий день уже поставлена.",
                FreezesAvailable = streak.FreezesAvailable,
                PendingFreezeDate = streak.PendingFreezeDate
            };
        }

        if (streak.FreezesAvailable <= 0)
        {
            return new StreakFreezeResponse
            {
                Success = false,
                Error = "В этом месяце не осталось заморозок.",
                FreezesAvailable = 0,
                PendingFreezeDate = null
            };
        }

        streak.PendingFreezeDate = today.AddDays(1);
        await _repository.SaveChangesAsync(cancellationToken);

        return new StreakFreezeResponse
        {
            Success = true,
            FreezesAvailable = streak.FreezesAvailable,
            PendingFreezeDate = streak.PendingFreezeDate
        };
    }

    public async Task<StreakLeaderboardResponse> GetLeaderboardAsync(
        string sort,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var byLongest = string.Equals(sort, "longest", StringComparison.OrdinalIgnoreCase);
        var today = Today();

        var rows = await _repository.GetLeaderboardAsync(byLongest, limit, today, cancellationToken);

        var entries = rows
            .Select((row, index) => new StreakLeaderboardEntryResponse
            {
                Rank = index + 1,
                Username = row.Username,
                CurrentStreak = row.CurrentStreak,
                LongestStreak = row.LongestStreak,
                IsStreakCreditedToday = row.IsCreditedToday
            })
            .ToList();

        return new StreakLeaderboardResponse
        {
            Entries = entries,
            Total = entries.Count
        };
    }

    public async Task<FortuneWheelSpinResponse> SpinFortuneWheelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var streak = await _repository.GetOrCreateStreakAsync(userId, cancellationToken);
        StreakLogic.ApplyRollover(streak, today);

        var rewards = await _repository.GetRewardsAsync(userId, cancellationToken);
        var unspun = rewards.FirstOrDefault(reward =>
            StreakLogic.IsWheelMilestone(reward.Milestone)
            && reward.RewardDescription == StreakLogic.WheelRewardDescription);

        if (unspun == null)
        {
            return new FortuneWheelSpinResponse
            {
                Success = false,
                Error = "Сейчас нет доступного вращения колеса фортуны."
            };
        }

        var (prize, index) = FortuneWheelPrizes.PickRandom();

        var claimed = await _repository.GetRewardAsync(userId, unspun.Milestone, cancellationToken);
        if (claimed == null)
        {
            return new FortuneWheelSpinResponse
            {
                Success = false,
                Error = "Награда за веху не найдена."
            };
        }

        claimed.RewardDescription = $"{StreakLogic.WheelRewardDescription} — {prize.Description}";

        var requiresManual = prize.Type == FortunePrizeType.Nft;
        if (requiresManual)
        {
            _repository.AddPendingManualReward(new PendingManualRewardEntity
            {
                UserId = userId,
                RewardType = "NFT",
                Status = "pending"
            });
        }
        else
        {
            await GrantPrizeAsync(userId, prize, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return new FortuneWheelSpinResponse
        {
            Success = true,
            PrizeType = FortuneWheelPrizes.PrizeTypeKey(prize.Type),
            PrizeDescription = prize.Description,
            PrizeIndex = index,
            RequiresManualAction = requiresManual,
            Message = requiresManual
                ? "Поздравляем! Напишите нам в Telegram, чтобы получить подарок."
                : $"Поздравляем! Вы выиграли: {prize.Description}.",
            SupportTelegramUrl = requiresManual ? _supportTelegramUrl : null
        };
    }

    public async Task MarkRewardsSeenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _repository.MarkRewardsSeenAsync(userId, cancellationToken);
    }

    private async Task<StreakActivityResultResponse> AddActiveMinutesAsync(
        Guid userId,
        double seconds,
        CancellationToken cancellationToken)
    {
        var today = Today();
        var minutes = (decimal)(Math.Max(0, seconds) / 60.0);
        if (minutes > MaxMinutesPerRequest)
        {
            minutes = MaxMinutesPerRequest;
        }

        var streak = await _repository.GetOrCreateStreakAsync(userId, cancellationToken);
        StreakLogic.ApplyRollover(streak, today);

        var progress = await _repository.GetOrCreateProgressAsync(userId, today, cancellationToken);
        progress.ActiveMinutes += minutes;

        return await CompleteProgressAsync(userId, streak, progress, today, cancellationToken);
    }

    private async Task<StreakActivityResultResponse> CompleteProgressAsync(
        Guid userId,
        UserStreakEntity streak,
        DailyActivityProgressEntity progress,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var creditedNow = false;
        int? newMilestone = null;
        string? newRewardDescription = null;
        var isWheel = false;

        if (!progress.IsStreakCredited
            && StreakLogic.HasReachedThreshold(progress.ActiveMinutes, progress.QualifyingCommentsCount))
        {
            progress.IsStreakCredited = true;
            StreakLogic.CreditDay(streak, today);
            creditedNow = true;

            (newMilestone, newRewardDescription, isWheel) =
                await ClaimReachedMilestonesAsync(userId, streak, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        var rewards = await _repository.GetRewardsAsync(userId, cancellationToken);
        var status = BuildStatus(streak, progress, rewards);

        return new StreakActivityResultResponse
        {
            StreakCreditedNow = creditedNow,
            NewRewardMilestone = newMilestone,
            NewRewardDescription = newRewardDescription,
            IsWheelReward = isWheel,
            Status = status
        };
    }

    private async Task<(int? Milestone, string? Description, bool IsWheel)> ClaimReachedMilestonesAsync(
        Guid userId,
        UserStreakEntity streak,
        CancellationToken cancellationToken)
    {
        int? resultMilestone = null;
        string? resultDescription = null;
        var resultIsWheel = false;

        foreach (var milestone in StreakLogic.Milestones)
        {
            if (streak.CurrentStreak < milestone)
            {
                break;
            }

            var existing = await _repository.GetRewardAsync(userId, milestone, cancellationToken);
            if (existing != null)
            {
                continue;
            }

            var description = StreakLogic.GetRewardDescription(milestone);
            _repository.AddReward(new StreakRewardsClaimedEntity
            {
                UserId = userId,
                Milestone = milestone,
                ClaimedAtUtc = DateTime.UtcNow,
                RewardDescription = description
            });

            if (StreakLogic.IsWheelMilestone(milestone))
            {
                resultMilestone = milestone;
                resultDescription = description;
                resultIsWheel = true;
            }
            else
            {
                await GrantMilestoneRewardAsync(userId, milestone, cancellationToken);
                resultMilestone = milestone;
                resultDescription = description;
                resultIsWheel = false;
            }
        }

        return (resultMilestone, resultDescription, resultIsWheel);
    }

    private async Task GrantMilestoneRewardAsync(
        Guid userId,
        int milestone,
        CancellationToken cancellationToken)
    {
        var vpnDays = StreakLogic.GetMilestoneVpnDays(milestone);
        var premiumDays = StreakLogic.GetMilestonePremiumDays(milestone);

        if (vpnDays > 0)
        {
            try
            {
                var (success, error, _) = await _vpnService.GrantDaysAsync(userId, vpnDays, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning(
                        "Стрик: не удалось начислить {Days} дней VPN пользователю {UserId}: {Error}",
                        vpnDays,
                        userId,
                        error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Стрик: сбой начисления VPN-дней пользователю {UserId}.", userId);
            }
        }

        if (premiumDays > 0)
        {
            try
            {
                await _premiumRewardClient.GrantPremiumDaysAsync(userId, premiumDays, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Стрик: сбой начисления премиум-дней пользователю {UserId}.", userId);
            }
        }
    }

    private async Task GrantPrizeAsync(
        Guid userId,
        FortunePrize prize,
        CancellationToken cancellationToken)
    {
        if (prize.VpnDays > 0)
        {
            try
            {
                var (success, error, _) = await _vpnService.GrantDaysAsync(userId, prize.VpnDays, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning(
                        "Колесо фортуны: не удалось начислить {Days} дней VPN пользователю {UserId}: {Error}",
                        prize.VpnDays,
                        userId,
                        error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Колесо фортуны: сбой начисления VPN-дней пользователю {UserId}.", userId);
            }
        }

        if (prize.PremiumDays > 0)
        {
            try
            {
                await _premiumRewardClient.GrantPremiumDaysAsync(userId, prize.PremiumDays, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Колесо фортуны: сбой начисления премиум-дней пользователю {UserId}.", userId);
            }
        }
    }

    private static StreakStatusResponse BuildStatus(
        UserStreakEntity? streak,
        DailyActivityProgressEntity? progress,
        IReadOnlyList<StreakRewardsClaimedEntity> rewards)
    {
        var current = streak?.CurrentStreak ?? 0;
        var longest = streak?.LongestStreak ?? 0;
        var activeMinutes = progress?.ActiveMinutes ?? 0m;
        var comments = progress?.QualifyingCommentsCount ?? 0;
        var total = StreakLogic.TotalMinutes(activeMinutes, comments);

        var claimedMilestones = rewards.Select(reward => reward.Milestone).ToHashSet();

        // Ближайшая недостигнутая (не полученная) веха.
        int? next = null;
        foreach (var milestone in StreakLogic.Milestones)
        {
            if (milestone > current && !claimedMilestones.Contains(milestone))
            {
                next = milestone;
                break;
            }
        }

        var milestones = StreakLogic.Milestones
            .Select(milestone => new StreakMilestoneResponse
            {
                Milestone = milestone,
                RewardDescription = StreakLogic.GetRewardDescription(milestone),
                IsWheel = StreakLogic.IsWheelMilestone(milestone),
                State = claimedMilestones.Contains(milestone) || current >= milestone
                    ? "achieved"
                    : milestone == next
                        ? "next"
                        : "locked"
            })
            .ToList();

        var pending = rewards
            .Where(reward => !reward.IsRewardSeen)
            .OrderBy(reward => reward.Milestone)
            .FirstOrDefault();

        return new StreakStatusResponse
        {
            CurrentStreak = current,
            LongestStreak = longest,
            IsStreakCreditedToday = progress?.IsStreakCredited ?? false,
            ActiveMinutesToday = activeMinutes,
            QualifyingCommentsToday = comments,
            TotalMinutesToday = total,
            ThresholdMinutes = StreakLogic.ThresholdMinutes,
            MinCommentWordCount = StreakLogic.MinCommentWordCount,
            MaxQualifyingCommentsPerDay = StreakLogic.MaxQualifyingComments,
            NextMilestone = next,
            DaysToNextMilestone = next.HasValue ? next.Value - current : null,
            NextMilestoneRewardDescription = next.HasValue ? StreakLogic.GetRewardDescription(next.Value) : null,
            FreezesAvailable = streak?.FreezesAvailable ?? StreakLogic.FreezesPerMonth,
            FreezesUsedThisMonth = streak?.FreezesUsedThisMonth ?? 0,
            PendingFreezeDate = streak?.PendingFreezeDate,
            CanFreeze = (streak?.FreezesAvailable ?? StreakLogic.FreezesPerMonth) > 0
                && streak?.PendingFreezeDate == null,
            Rewards = rewards
                .Select(reward => new StreakRewardItemResponse
                {
                    Milestone = reward.Milestone,
                    ClaimedAtUtc = reward.ClaimedAtUtc,
                    RewardDescription = reward.RewardDescription
                })
                .ToList(),
            Milestones = milestones,
            PendingReward = pending == null
                ? null
                : new StreakPendingRewardResponse
                {
                    Milestone = pending.Milestone,
                    RewardDescription = pending.RewardDescription,
                    IsWheel = StreakLogic.IsWheelMilestone(pending.Milestone)
                }
        };
    }
}
