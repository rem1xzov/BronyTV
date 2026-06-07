using BronyTV.DbContext.Configuration;
using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;

namespace BronyTV.DbContext;

public class DbBronyTV : Microsoft.EntityFrameworkCore.DbContext
{
    public DbBronyTV(DbContextOptions<DbBronyTV> options) : base(options)
    {
    }
    
    public DbSet<SeasonEntity> Seasons => Set<SeasonEntity>();
    public DbSet<VideoEntity> Videos => Set<VideoEntity>();
    public DbSet<AdminEntity> Admins => Set<AdminEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CommentEntity> Comments => Set<CommentEntity>();
    public DbSet<CommentLikeEntity> CommentLikes => Set<CommentLikeEntity>();
    public DbSet<ForumThreadEntity> ForumThreads => Set<ForumThreadEntity>();
    public DbSet<ForumPostEntity> ForumPosts => Set<ForumPostEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfiguration(new SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new VideoConfiguration());
        modelBuilder.ApplyConfiguration(new AdminConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new CommentConfiguration());
        modelBuilder.ApplyConfiguration(new CommentLikeConfiguration());
        modelBuilder.ApplyConfiguration(new ForumThreadConfiguration());
        modelBuilder.ApplyConfiguration(new ForumPostConfiguration());
    }
}