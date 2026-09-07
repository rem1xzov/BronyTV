using Microsoft.EntityFrameworkCore;

namespace AiBronyTV.Core;

public class UserLimitEntity
{
    public string SessionId { get; set; } = null!;
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public DateTime? PremiumUntil { get; set; }
}

public class PremiumKeyEntity
{
    public string Key { get; set; } = null!;
    public bool IsUsed { get; set; }
}

public class ChatMessageEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = null!;
    public string CharacterId { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    /// <summary>true — сообщение админского раздела ИИ-ботов; false — публичного.</summary>
    public bool IsAdminChat { get; set; }
}

/// <summary>
/// Закреплённый чат (персонаж) конкретного пользователя. Ключ — UserId + CharacterId,
/// поэтому у пользователя не более одной записи на персонажа. <c>IsPinned=false</c>
/// означает «откреплён» (строка остаётся, чтобы не плодить пустые удаления), а
/// <c>PinnedAt</c> используется для сортировки закреплённых чатов (последний — выше).
/// </summary>
public class PinnedChatEntity
{
    public string UserId { get; set; } = null!;
    public string CharacterId { get; set; } = null!;
    public bool IsPinned { get; set; }
    public DateTime PinnedAt { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserLimitEntity> UserLimits => Set<UserLimitEntity>();
    public DbSet<PremiumKeyEntity> PremiumKeys => Set<PremiumKeyEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<PinnedChatEntity> PinnedChats => Set<PinnedChatEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ai");

        modelBuilder.Entity<UserLimitEntity>(entity =>
        {
            entity.ToTable("UserLimits", "ai");
            entity.HasKey(item => item.SessionId);
            entity.Property(item => item.SessionId).HasMaxLength(64);
        });

                modelBuilder.Entity<PremiumKeyEntity>(entity =>
        {
            entity.ToTable("PremiumKeys", "ai");
            entity.HasKey(key => key.Key);
            entity.Property(key => key.Key).HasMaxLength(32).IsRequired();
            entity.Property(key => key.IsUsed).IsRequired();
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.ToTable("ChatMessages", "ai");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SessionId).HasMaxLength(170);
            entity.Property(message => message.CharacterId).HasMaxLength(32);
            entity.Property(message => message.Role).HasMaxLength(16);
            entity.Property(message => message.IsAdminChat).IsRequired().HasDefaultValue(false);
            entity.HasIndex(message => new { message.SessionId, message.CharacterId, message.Timestamp });
        });

        modelBuilder.Entity<PinnedChatEntity>(entity =>
        {
            entity.ToTable("PinnedChats", "ai");
            entity.HasKey(pinned => new { pinned.UserId, pinned.CharacterId });
            entity.Property(pinned => pinned.UserId).HasMaxLength(64);
            entity.Property(pinned => pinned.CharacterId).HasMaxLength(32);
            entity.Property(pinned => pinned.IsPinned).IsRequired().HasDefaultValue(true);
            entity.Property(pinned => pinned.PinnedAt).IsRequired();
        });
    }
}
