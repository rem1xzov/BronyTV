using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class UserActivityConfiguration : IEntityTypeConfiguration<UserActivityEntity>
{
    public void Configure(EntityTypeBuilder<UserActivityEntity> builder)
    {
        builder.ToTable("UserActivities", "public");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id).ValueGeneratedOnAdd();

        builder.Property(activity => activity.ActivityType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(activity => activity.Details)
            .HasMaxLength(200);

        builder.Property(activity => activity.Timestamp)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(activity => new { activity.UserId, activity.Timestamp });
    }
}
