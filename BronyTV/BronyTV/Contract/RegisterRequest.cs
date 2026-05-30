using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Race { get; set; } = string.Empty;
}
