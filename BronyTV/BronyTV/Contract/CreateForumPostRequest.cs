using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateForumPostRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
