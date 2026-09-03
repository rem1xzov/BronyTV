using System;

namespace BronyTV.DbContext.Entity;

/// <summary>
/// Награда, требующая ручной обработки администратором (например, NFT-подарок в Telegram).
/// Автоматическая выдача для таких наград не выполняется — админ обрабатывает вручную.
/// </summary>
public class PendingManualRewardEntity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Тип награды (например "NFT").</summary>
    public string RewardType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Статус обработки: pending / processed.</summary>
    public string Status { get; set; } = "pending";
}
