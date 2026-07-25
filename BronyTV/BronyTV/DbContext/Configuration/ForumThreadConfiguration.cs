using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class ForumThreadConfiguration : IEntityTypeConfiguration<ForumThreadEntity>
{
    public void Configure(EntityTypeBuilder<ForumThreadEntity> builder)
    {
        builder.ToTable("ForumThreads", "public");
        builder.HasKey(thread => thread.Id);

        builder.Property(thread => thread.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(thread => thread.Description)
            .HasMaxLength(4000);

        builder.Property(thread => thread.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(thread => thread.CreatedAtUtc);

        builder.HasOne(thread => thread.Author)
            .WithMany()
            .HasForeignKey(thread => thread.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
