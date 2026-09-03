using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class UserStreakConfiguration : IEntityTypeConfiguration<UserStreakEntity>
{
    public void Configure(EntityTypeBuilder<UserStreakEntity> builder)
    {
        builder.ToTable("UserStreaks", "public");
        builder.HasKey(streak => streak.UserId);

        builder.Property(streak => streak.CurrentStreak)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(streak => streak.LongestStreak)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(streak => streak.LastActiveDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(streak => streak.FreezesAvailable)
            .HasDefaultValue(3)
            .IsRequired();

        builder.Property(streak => streak.FreezesUsedThisMonth)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(streak => streak.FreezesMonth)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(streak => streak.PendingFreezeDate)
            .HasColumnType("date")
            .IsRequired(false);

        builder.HasOne(streak => streak.User)
            .WithOne()
            .HasForeignKey<UserStreakEntity>(streak => streak.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
