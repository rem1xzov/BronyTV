using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateCommentRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;
}
