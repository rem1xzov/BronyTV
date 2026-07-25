using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLikeEntity>
{
    public void Configure(EntityTypeBuilder<CommentLikeEntity> builder)
    {
        builder.ToTable("CommentLikes", "public");
        builder.HasKey(like => new { like.UserId, like.CommentId });

        builder.Property(like => like.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(like => like.CommentId);

        builder.HasOne(like => like.User)
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(like => like.Comment)
            .WithMany(comment => comment.Likes)
            .HasForeignKey(like => like.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
