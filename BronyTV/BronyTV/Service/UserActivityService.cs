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
    private readonly IUserRepository _userRepository;

    public UserActivityService(
        IUserActivityRepository repository,
        IUserRepository userRepository)
    {
        _repository = repository;
        _userRepository = userRepository;
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

        // Простое окно хранения (Часть 4): при каждой записи подчищаем записи старше 7 дней,
        // чтобы таблица не росла бесконечно. Объёмы проекта небольшие (десятки-сотни
        // пользователей), поэтому одиночный DELETE на запись — приемлемая цена за отказ
        // от фоновых задач/Hosted Services.
        await _repository.DeleteOlderThanAsync(TimeSpan.FromDays(7), CancellationToken.None);
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

    public async Task<IReadOnlyList<UserActivityWithUserResponse>> GetRecentAllUsersAsync(
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetRecentAllUsersAsync(days, 500, cancellationToken);

        var userIds = items.Select(item => item.UserId).Distinct().ToList();
        var usersById = new Dictionary<Guid, (string? Username, string? Email)>();
        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            usersById[userId] = (user?.Username, user?.Email);
        }

        return items
            .Select(activity =>
            {
                usersById.TryGetValue(activity.UserId, out var user);
                return new UserActivityWithUserResponse
                {
                    Id = activity.Id,
                    UserId = activity.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    Type = activity.ActivityType,
                    Details = activity.Details,
                    Timestamp = activity.Timestamp
                };
            })
            .ToList();
    }

    public async Task<bool> HideAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _repository.HideFromAdminAsync(id, cancellationToken);
    }
}
