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

/// <summary>Групповой чат с несколькими ботами-персонажами одновременно.</summary>
public class GroupChatEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    /// <summary>true — чат админского раздела; false — публичного.</summary>
    public bool IsAdminChat { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Участник (бот-персонаж) группового чата.</summary>
public class GroupChatParticipantEntity
{
    public int GroupChatId { get; set; }
    public string CharacterId { get; set; } = null!;
    public DateTime AddedAt { get; set; }
}

/// <summary>Сообщение в групповом чате (от пользователя или от бота).</summary>
public class GroupChatMessageEntity
{
    public int Id { get; set; }
    public int GroupChatId { get; set; }
    public string SenderType { get; set; } = null!;
    public string? SenderCharacterId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
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
    public DbSet<GroupChatEntity> GroupChats => Set<GroupChatEntity>();
    public DbSet<GroupChatParticipantEntity> GroupChatParticipants => Set<GroupChatParticipantEntity>();
    public DbSet<GroupChatMessageEntity> GroupChatMessages => Set<GroupChatMessageEntity>();

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

        modelBuilder.Entity<GroupChatEntity>(entity =>
        {
            entity.ToTable("GroupChats", "ai");
            entity.HasKey(chat => chat.Id);
            entity.Property(chat => chat.UserId).HasMaxLength(64).IsRequired();
            entity.Property(chat => chat.Name).HasMaxLength(200).IsRequired();
            entity.Property(chat => chat.IsAdminChat).IsRequired().HasDefaultValue(false);
            entity.Property(chat => chat.CreatedAt).IsRequired();
            entity.HasIndex(chat => new { chat.UserId, chat.IsAdminChat });
        });

        modelBuilder.Entity<GroupChatParticipantEntity>(entity =>
        {
            entity.ToTable("GroupChatParticipants", "ai");
            entity.HasKey(participant => new { participant.GroupChatId, participant.CharacterId });
            entity.Property(participant => participant.CharacterId).HasMaxLength(32).IsRequired();
            entity.Property(participant => participant.AddedAt).IsRequired();
        });

        modelBuilder.Entity<GroupChatMessageEntity>(entity =>
        {
            entity.ToTable("GroupChatMessages", "ai");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SenderType).HasMaxLength(16).IsRequired();
            entity.Property(message => message.SenderCharacterId).HasMaxLength(32);
            entity.Property(message => message.Content).IsRequired();
            entity.Property(message => message.CreatedAt).IsRequired();
            entity.HasIndex(message => new { message.GroupChatId, message.CreatedAt });
        });
    }
}
