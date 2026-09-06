using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class NewsCommentConfiguration : IEntityTypeConfiguration<NewsCommentEntity>
{
    public void Configure(EntityTypeBuilder<NewsCommentEntity> builder)
    {
        builder.ToTable("NewsComments", "public");
        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Content)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(comment => comment.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(comment => comment.NewsId);
        builder.HasIndex(comment => comment.CreatedAtUtc);
        builder.HasIndex(comment => comment.ReplyToCommentId);

        builder.HasOne(comment => comment.News)
            .WithMany()
            .HasForeignKey(comment => comment.NewsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.Author)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.ReplyToComment)
            .WithMany()
            .HasForeignKey(comment => comment.ReplyToCommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
