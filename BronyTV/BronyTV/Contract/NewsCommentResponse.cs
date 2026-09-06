using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

/// <summary>Комментарий к новости (схема идентична комментарию форума).</summary>
public class NewsCommentResponse
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = "user";
    public Guid? ReplyToCommentId { get; set; }
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

public class CreateNewsCommentRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public List<string>? Images { get; set; }

    public Guid? ReplyToCommentId { get; set; }
}
