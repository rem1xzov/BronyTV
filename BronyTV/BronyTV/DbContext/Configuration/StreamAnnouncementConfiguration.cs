using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class StreamAnnouncementConfiguration : IEntityTypeConfiguration<StreamAnnouncementEntity>
{
    public void Configure(EntityTypeBuilder<StreamAnnouncementEntity> builder)
    {
        builder.ToTable("StreamAnnouncements", "public");
        builder.HasKey(announcement => announcement.Id);

        builder.Property(announcement => announcement.Status)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(announcement => announcement.ScheduledAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(announcement => announcement.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(announcement => announcement.Status);
        builder.HasIndex(announcement => announcement.ScheduledAtUtc);

        builder.HasOne(announcement => announcement.Video)
            .WithMany()
            .HasForeignKey(announcement => announcement.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(announcement => announcement.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(announcement => announcement.CreatedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
