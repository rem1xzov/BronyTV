using System;
using System.Threading;
using System.Threading.Tasks;
using BronyTV.Contract;

namespace BronyTV.Service;

/// <summary>
/// Элемент активности с именем автора (для страницы активности в админке).
/// </summary>
public class UserActivityWithUserResponse
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

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

    /// <summary>
    /// Вся активность ВСЕХ пользователей за последние N дней (по убыванию времени),
    /// вместе с username/email автора. Используется отдельной страницей админки.
    /// </summary>
    Task<IReadOnlyList<UserActivityWithUserResponse>> GetRecentAllUsersAsync(
        int days = 7,
        CancellationToken cancellationToken = default);
}
