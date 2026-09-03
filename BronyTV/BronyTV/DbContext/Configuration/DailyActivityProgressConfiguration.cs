using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class DailyActivityProgressConfiguration : IEntityTypeConfiguration<DailyActivityProgressEntity>
{
    public void Configure(EntityTypeBuilder<DailyActivityProgressEntity> builder)
    {
        builder.ToTable("DailyActivityProgress", "public");
        builder.HasKey(progress => new { progress.UserId, progress.Date });

        builder.Property(progress => progress.Date)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(progress => progress.ActiveMinutes)
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(progress => progress.QualifyingCommentsCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(progress => progress.IsStreakCredited)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
