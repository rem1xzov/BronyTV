using System;
using System.Collections.Generic;

namespace BronyTV.DbContext.Entity;

public class ForumPostEntity
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public ForumThreadEntity Thread { get; set; } = null!;
    public UserEntity Author { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Images { get; set; }
}
