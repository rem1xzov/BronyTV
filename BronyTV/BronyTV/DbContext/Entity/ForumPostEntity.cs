using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BronyTV.DbContext.Entity;

public class ForumPostEntity
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
        public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public Guid? ReplyToPostId { get; set; }
    public ForumThreadEntity Thread { get; set; } = null!;
    public UserEntity Author { get; set; } = null!;
    public ForumPostEntity? ReplyToPost { get; set; }
    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "text")]
    public string? Images { get; set; }
    [Column(TypeName = "text")]
    public string? LikedUserIds { get; set; }
}
