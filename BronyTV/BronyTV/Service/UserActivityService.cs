using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;

namespace BronyTV.Service;

public class UserActivityService : IUserActivityService
{
    private readonly IUserActivityRepository _repository;

    public UserActivityService(IUserActivityRepository repository)
    {
        _repository = repository;
    }

    // Сколько времени одно и то же событие (тип + детали) считается "свежим" и
    // не должно записываться повторно. Защищает от спама (например, от пула
    // range-запросов стриминга видео, перезагрузок и быстрых повторов действий).
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(5);

    public async Task RecordAsync(
        Guid userId,
        string activityType,
        string? details,
        CancellationToken cancellationToken = default)
    {
        // Always cap details to 200 characters (server-side guard against abuse).
        var safeDetails = details?.Trim();
        if (!string.IsNullOrEmpty(safeDetails) && safeDetails.Length > 200)
        {
            safeDetails = safeDetails.Substring(0, 200);
        }

        // Если такое же событие уже зафиксировано совсем недавно — пропускаем,
        // чтобы не засорять историю дублями.
        if (await _repository.HasRecentAsync(
                userId,
                activityType,
                safeDetails,
                DedupeWindow,
                cancellationToken))
        {
            return;
        }

        var activity = new UserActivityEntity
        {
            UserId = userId,
            ActivityType = activityType,
            Details = string.IsNullOrWhiteSpace(safeDetails) ? null : safeDetails,
            Timestamp = DateTime.UtcNow
        };

        await _repository.AddAsync(activity, cancellationToken);
    }

    public async Task<UserActivityListResponse> GetRecentAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetRecentAsync(userId, limit, cancellationToken);

        return new UserActivityListResponse
        {
            Activities = items
                .Select(activity => new UserActivityItemResponse
                {
                    Type = activity.ActivityType,
                    Details = activity.Details,
                    Timestamp = activity.Timestamp
                })
                .ToList()
        };
    }
}
