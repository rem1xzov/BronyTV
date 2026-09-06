using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BronyTV.DbContext.Entity;

/// <summary>
/// Комментарий к новости. Зеркалирует <see cref="ForumPostEntity"/>, чтобы
/// поведение комментариев новостей совпадало с форумом 1 в 1 (текст, картинки,
/// лайки, ответы на комментарий, огонёк стрика автора).
/// </summary>
public class NewsCommentEntity
{
    public Guid Id { get; set; }
    public Guid NewsId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public Guid? ReplyToCommentId { get; set; }
    public NewsPost News { get; set; } = null!;
    public UserEntity Author { get; set; } = null!;
    public NewsCommentEntity? ReplyToComment { get; set; }
    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "text")]
    public string? Images { get; set; }
    [Column(TypeName = "text")]
    public string? LikedUserIds { get; set; }
}
