using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class ForumPostConfiguration : IEntityTypeConfiguration<ForumPostEntity>
{
    public void Configure(EntityTypeBuilder<ForumPostEntity> builder)
    {
        builder.ToTable("ForumPosts", "public");
        builder.HasKey(post => post.Id);

        builder.Property(post => post.Content)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(post => post.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(post => post.ThreadId);
        builder.HasIndex(post => post.CreatedAtUtc);

        builder.HasOne(post => post.Thread)
            .WithMany(thread => thread.Posts)
            .HasForeignKey(post => post.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(post => post.Author)
            .WithMany()
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
