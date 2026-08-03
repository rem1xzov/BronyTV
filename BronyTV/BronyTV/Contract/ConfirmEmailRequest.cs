using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class ConfirmEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
