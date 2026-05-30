using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class GoogleLoginRequest
{
    [Required]
    [MaxLength(8192)]
    public string IdToken { get; set; } = string.Empty;
}
