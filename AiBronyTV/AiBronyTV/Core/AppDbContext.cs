using Microsoft.EntityFrameworkCore;

namespace AiBronyTV.Core;

public class UserLimitEntity
{
    public string SessionId { get; set; } = null!;
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class ChatMessageEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = null!;
    public string CharacterId { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserLimitEntity> UserLimits => Set<UserLimitEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ai");

        modelBuilder.Entity<UserLimitEntity>(entity =>
        {
            entity.ToTable("UserLimits", "ai");
            entity.HasKey(item => item.SessionId);
            entity.Property(item => item.SessionId).HasMaxLength(64);
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.ToTable("ChatMessages", "ai");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SessionId).HasMaxLength(170);
            entity.Property(message => message.CharacterId).HasMaxLength(32);
            entity.Property(message => message.Role).HasMaxLength(16);
            entity.HasIndex(message => new { message.SessionId, message.CharacterId, message.Timestamp });
        });
    }
}
