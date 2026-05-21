using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public record LoginRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Username,
    [Required, StringLength(200, MinimumLength = 1)] string Password);