using System;
using System.Collections.Generic;

namespace BronyTV.Contract;

public class ForumPostResponse
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = "user";
    public DateTime CreatedAtUtc { get; set; }
    public List<string>? Images { get; set; }
    public int Likes { get; set; }
    public bool LikedByMe { get; set; }
}
