using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class SelectRaceRequest
{
    [Required]
    [MaxLength(32)]
    public string Race { get; set; } = string.Empty;
}
