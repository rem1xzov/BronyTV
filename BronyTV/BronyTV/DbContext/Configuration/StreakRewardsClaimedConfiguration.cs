using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class StreakRewardsClaimedConfiguration : IEntityTypeConfiguration<StreakRewardsClaimedEntity>
{
    public void Configure(EntityTypeBuilder<StreakRewardsClaimedEntity> builder)
    {
        builder.ToTable("StreakRewardsClaimed", "public");
        builder.HasKey(reward => new { reward.UserId, reward.Milestone });

        builder.Property(reward => reward.Milestone)
            .IsRequired();

        builder.Property(reward => reward.ClaimedAtUtc)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(reward => reward.RewardDescription)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(reward => reward.IsRewardSeen)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasOne(reward => reward.User)
            .WithMany()
            .HasForeignKey(reward => reward.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
