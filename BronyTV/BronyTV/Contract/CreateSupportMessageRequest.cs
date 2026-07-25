using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateSupportMessageRequest
{
    [MaxLength(4000)]
    public string? Content { get; set; }

    [MaxLength(4000)]
    public string? Text { get; set; }
}
