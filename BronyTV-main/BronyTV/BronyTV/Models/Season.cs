namespace BronyTV.Models;

public class Season
{
    public Guid Id { get; set; }
    public int Number { get; set; } //Номер сезона
    public string Title { get; set; } = string.Empty;//Название сезона
    public string Description { get; set; } = string.Empty;//Описание сезона
    public string? PosterPath { get; set; }
}