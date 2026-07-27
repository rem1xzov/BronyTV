using System;
using System.Collections.Generic;

namespace BronyTV.DbContext.Entity;

public class ForumThreadEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AuthorId { get; set; }
    public UserEntity Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string>? Images { get; set; } = new();
    public ICollection<ForumPostEntity> Posts { get; set; } = new List<ForumPostEntity>();
}
