using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class ReferralRewardConfiguration : IEntityTypeConfiguration<ReferralRewardEntity>
{
    public void Configure(EntityTypeBuilder<ReferralRewardEntity> builder)
    {
        builder.ToTable("ReferralRewards", "public");
        builder.HasKey(reward => reward.Id);

        builder.Property(reward => reward.Reason)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(reward => reward.BonusDays)
            .IsRequired();

        builder.Property(reward => reward.IsRedeemed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(reward => reward.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(reward => reward.ReferrerId);
        builder.HasIndex(reward => reward.ReferralUserId);

        builder.HasOne(reward => reward.Referrer)
            .WithMany()
            .HasForeignKey(reward => reward.ReferrerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(reward => reward.ReferralUser)
            .WithMany()
            .HasForeignKey(reward => reward.ReferralUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
