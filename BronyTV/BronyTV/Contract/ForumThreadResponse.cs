using System;
using System.Collections.Generic;

namespace BronyTV.Contract;

public class ForumThreadResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int PostCount { get; set; }
    public List<string>? Images { get; set; }
}
