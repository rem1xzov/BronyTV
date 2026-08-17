using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BronyTV.DbContext.Entity;

public class UserActivityEntity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }

    // "video_watch" | "bot_chat" | "forum_view" | "forum_post" | "news_view"
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Короткое описание (название серии/темы/новости, имя бота), ≤200 символов.
    /// Для бота хранится только имя персонажа — НИКОГДА не текст сообщения.
    /// </summary>
    public string? Details { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
