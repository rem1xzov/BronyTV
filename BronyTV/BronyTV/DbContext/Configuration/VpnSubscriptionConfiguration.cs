using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class VpnSubscriptionConfiguration : IEntityTypeConfiguration<VpnSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<VpnSubscriptionEntity> builder)
    {
        builder.ToTable("VpnSubscriptions", "public");
        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Kind)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(subscription => subscription.PlanName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(subscription => subscription.StartedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(subscription => subscription.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(subscription => subscription.ClientUuid)
            .HasMaxLength(64);

        builder.Property(subscription => subscription.Note)
            .HasMaxLength(500);

        builder.Property(subscription => subscription.IsRevoked)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(subscription => subscription.PanelPlanNameId)
            .HasMaxLength(32);

        builder.HasIndex(subscription => subscription.UserId);

        builder.HasOne(subscription => subscription.User)
            .WithMany()
            .HasForeignKey(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
