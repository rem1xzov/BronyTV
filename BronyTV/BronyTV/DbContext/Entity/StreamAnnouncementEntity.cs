namespace BronyTV.DbContext.Entity;

/// <summary>
/// Анонс совместного просмотра (WatchParty): администратор заранее планирует стрим
/// конкретного видео с точным временем старта. Переживает перезапуск бэкенда (в отличие
/// от эфемерного состояния самого эфира, которое живёт в памяти).
/// </summary>
public class StreamAnnouncementEntity
{
    public Guid Id { get; set; }

    /// <summary>Ссылка на существующее видео каталога (серия/фильм).</summary>
    public Guid VideoId { get; set; }
    public VideoEntity Video { get; set; } = null!;

    /// <summary>Точное время запланированного старта (UTC).</summary>
    public DateTimeOffset ScheduledAtUtc { get; set; }

    /// <summary>Статус: scheduled | completed | cancelled.</summary>
    public string Status { get; set; } = "scheduled";

    public Guid? CreatedByAdminId { get; set; }
    public UserEntity? CreatedByAdmin { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
