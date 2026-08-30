using System;

namespace BronyTV.Contract;

/// <summary>Анонс стрима для фронтенда (список прошедших/будущих).</summary>
public class StreamAnnouncementResponse
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public string VideoTitle { get; set; } = string.Empty;
    public int? SeasonNumber { get; set; }
    public string? SeasonTitle { get; set; }
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public string Status { get; set; } = "scheduled";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class CreateStreamAnnouncementRequest
{
    public Guid VideoId { get; set; }
    public DateTimeOffset ScheduledAtUtc { get; set; }
}

/// <summary>Снимок состояния текущего эфира, рассылаемый клиентам.</summary>
public class WatchPartySyncState
{
    public bool IsLive { get; set; }
    public Guid? VideoId { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoTitle { get; set; }
    public bool IsPaused { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public double PausedAtSeconds { get; set; }
    public double PositionSeconds { get; set; }
    public DateTimeOffset ServerTimeUtc { get; set; }
}

/// <summary>Сообщение чата стрима (в памяти, не персистится).</summary>
public class WatchPartyChatMessage
{
    public string Username { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
    public bool IsSystem { get; set; }
}
