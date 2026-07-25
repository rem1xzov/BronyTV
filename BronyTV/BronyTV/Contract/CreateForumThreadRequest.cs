using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateForumThreadRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }
}
