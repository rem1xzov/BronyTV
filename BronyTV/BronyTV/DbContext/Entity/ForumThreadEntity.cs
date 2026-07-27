using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BronyTV.DbContext.Entity;

public class ForumThreadEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AuthorId { get; set; }
    public UserEntity Author { get; set; } = null!;
    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "text")]
    public string? Images { get; set; }
    public ICollection<ForumPostEntity> Posts { get; set; } = new List<ForumPostEntity>();
}
