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
    public string Role { get; set; } = null!; // "user" или "assistant"
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserLimitEntity> UserLimits { get; set; }
    public DbSet<ChatMessageEntity> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserLimitEntity>().HasKey(u => u.SessionId);
        modelBuilder.Entity<ChatMessageEntity>().HasKey(m => m.Id);
    }
}