using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class RequestPasswordResetRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}
