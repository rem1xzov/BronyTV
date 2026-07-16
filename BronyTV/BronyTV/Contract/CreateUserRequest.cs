using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateUserRequest
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
    [MinLength(4)]
    [MaxLength(25)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "User";

    public string? Race { get; set; }
}
