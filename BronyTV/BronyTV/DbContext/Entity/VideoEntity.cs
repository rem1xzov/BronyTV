namespace BronyTV.DbContext.Entity;

public class VideoEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int EpisodeNumber { get; set; } 
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? PreviewImageUrl { get; set; }
    public Guid SeasonId { get; set; }
    public SeasonEntity Season { get; set; } = null!;
}