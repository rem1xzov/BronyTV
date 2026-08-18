using BronyTV.DbContext.Entity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class UserFavoriteConfiguration : IEntityTypeConfiguration<UserFavoriteEntity>
{
    public void Configure(EntityTypeBuilder<UserFavoriteEntity> builder)
    {
        builder.ToTable("UserFavorites", "public");
        builder.HasKey(favorite => favorite.Id);

        builder.HasIndex(favorite => new { favorite.UserId, favorite.VideoId })
            .IsUnique();

        builder.Property(favorite => favorite.AddedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne(favorite => favorite.User)
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite => favorite.Video)
            .WithMany()
            .HasForeignKey(favorite => favorite.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
