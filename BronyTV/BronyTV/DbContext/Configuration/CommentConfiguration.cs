using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class CommentConfiguration : IEntityTypeConfiguration<CommentEntity>
{
    public void Configure(EntityTypeBuilder<CommentEntity> builder)
    {
        builder.ToTable("Comments", "public");
        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(comment => comment.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(comment => comment.VideoId);
        builder.HasIndex(comment => comment.UserId);
        builder.HasIndex(comment => comment.ParentCommentId);

        builder.HasOne(comment => comment.ParentComment)
            .WithMany(parent => parent.Replies)
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.Video)
            .WithMany()
            .HasForeignKey(comment => comment.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.User)
            .WithMany()
            .HasForeignKey(comment => comment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
