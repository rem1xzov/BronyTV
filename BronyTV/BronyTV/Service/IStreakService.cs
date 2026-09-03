using System;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;

namespace BronyTV.Service;

public interface IStreakService
{
    Task<StreakStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Записывает секунды активного просмотра видео.</summary>
    Task<StreakActivityResultResponse> RecordVideoWatchAsync(
        Guid userId,
        double seconds,
        CancellationToken cancellationToken = default);

    /// <summary>Записывает секунды активного диалога с ИИ-ботом.</summary>
    Task<StreakActivityResultResponse> RecordBotChatAsync(
        Guid userId,
        double seconds,
        CancellationToken cancellationToken = default);

    /// <summary>Записывает комментарий на форуме (засчитывается только если ≥5 слов).</summary>
    Task<StreakActivityResultResponse> RecordForumCommentAsync(
        Guid userId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>Ставит заморозку на следующий день.</summary>
    Task<StreakFreezeResponse> SetFreezeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<StreakLeaderboardResponse> GetLeaderboardAsync(
        string sort,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Вращает колесо фортуны (исход решается на сервере).</summary>
    Task<FortuneWheelSpinResponse> SpinFortuneWheelAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Помечает непоказанные награды пользователя как показанные (после модалки).</summary>
    Task MarkRewardsSeenAsync(Guid userId, CancellationToken cancellationToken = default);
}
