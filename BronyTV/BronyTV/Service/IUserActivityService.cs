using System;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;

namespace BronyTV.Service;

public interface IUserActivityService
{
    /// <summary>
    /// Записывает факт действия пользователя. Для бота передаётся только имя персонажа,
    /// НИКОГДА не содержимое сообщения.
    /// </summary>
    Task RecordAsync(
        Guid userId,
        string activityType,
        string? details,
        CancellationToken cancellationToken = default);

    Task<UserActivityListResponse> GetRecentAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
