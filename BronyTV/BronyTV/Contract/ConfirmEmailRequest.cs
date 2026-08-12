using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class ConfirmEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$")]
    public string Token { get; set; } = string.Empty;
}
