namespace BronyTV.DbContext.Entity;

public class SeasonEntity
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    
    public ICollection<VideoEntity> Videos { get; set; } = new List<VideoEntity>();
}