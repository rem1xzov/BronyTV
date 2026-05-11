namespace BronyTV.Models;

public class Video
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty; //Название серии
    public int EpisodeNumber { get; set; } //Номер серии
    public string FilePath { get; set; } = string.Empty;//Путь,где лежит видео на сервере
    public string? PreviewImageUrl { get; set; } //Ссылка на кадр из серии (превью)
    public string Description { get; set; } = string.Empty;
    
    //Ссылка на сезон
    public Guid SeasonId { get; set; }
}