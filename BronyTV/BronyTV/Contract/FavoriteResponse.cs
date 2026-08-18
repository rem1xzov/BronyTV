using System;

namespace BronyTV.Contract;

/// <summary>
/// Одна закладка в списке «Избранное». Содержит всё необходимое, чтобы
/// перейти к нужной серии на плеере (/player/{season}/{episode}).
/// </summary>
public class FavoriteItemResponse
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>
/// Тонкий ответ о том, отмечено ли видео как избранное у текущего пользователя.
/// </summary>
public class FavoriteStatusResponse
{
    public bool IsFavorite { get; set; }
}
