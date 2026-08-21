using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class VpnPromoKeyConfiguration : IEntityTypeConfiguration<VpnPromoKeyEntity>
{
    public void Configure(EntityTypeBuilder<VpnPromoKeyEntity> builder)
    {
        builder.ToTable("VpnPromoKeys", "public");
        builder.HasKey(promo => promo.Code);

        builder.Property(promo => promo.Code)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(promo => promo.IsUsed)
            .IsRequired();

        builder.Property(promo => promo.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(promo => promo.UsedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(promo => promo.ClientUuid)
            .HasMaxLength(64);

        builder.Property(promo => promo.DurationMonths)
            .IsRequired();

        builder.HasIndex(promo => promo.IsUsed);

        builder.HasOne(promo => promo.UsedByUser)
            .WithMany()
            .HasForeignKey(promo => promo.UsedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(promo => promo.Subscription)
            .WithMany()
            .HasForeignKey(promo => promo.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
