using System;
using System.Collections.Generic;

namespace BronyTV.Contract;

public class ForumPostResponse
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = "user";
    public Guid? ReplyToPostId { get; set; }
    public string? ReplyToAuthorUsername { get; set; }
    public string? ReplyToContent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<string>? Images { get; set; }
    public int Likes { get; set; }
    public bool LikedByMe { get; set; }

    /// <summary>Текущий стрик автора (для огонька у никнейма).</summary>
    public int AuthorStreak { get; set; }

    /// <summary>Засчитан ли у автора сегодняшний день (огонёк «горит»).</summary>
    public bool AuthorStreakActive { get; set; }
}
