using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.DbContext.Entity;

namespace BronyTV.Repository;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivityEntity activity, CancellationToken cancellationToken = default);
    Task<bool> HasRecentAsync(
        Guid userId,
        string activityType,
        string? details,
        TimeSpan within,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserActivityEntity>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает всю активность всех пользователей за последние N дней
    /// (по убыванию времени).
    /// </summary>
    Task<IReadOnlyList<UserActivityEntity>> GetRecentAllUsersAsync(
        int days = 7,
        int limit = 500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Мягко скрывает запись активности из админ-ленты (без физического удаления).
    /// Возвращает true, если запись найдена и помечена.
    /// </summary>
    Task<bool> HideFromAdminAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Мягко скрывает ВСЕ записи активности указанного пользователя из админ-ленты
    /// (без физического удаления). Возвращает количество скрытых записей.
    /// </summary>
    Task<int> HideAllFromAdminAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет записи активности старше указанного возраста (окно хранения).
    /// Возвращает количество удалённых строк.
    /// </summary>
    Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}
