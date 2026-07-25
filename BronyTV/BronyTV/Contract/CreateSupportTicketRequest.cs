using System.ComponentModel.DataAnnotations;

namespace BronyTV.Contract;

public class CreateSupportTicketRequest
{
    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(150)]
    public string? Subject { get; set; }

    [MaxLength(4000)]
    public string? Content { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }
}
