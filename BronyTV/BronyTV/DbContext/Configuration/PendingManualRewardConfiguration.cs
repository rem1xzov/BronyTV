using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class PendingManualRewardConfiguration : IEntityTypeConfiguration<PendingManualRewardEntity>
{
    public void Configure(EntityTypeBuilder<PendingManualRewardEntity> builder)
    {
        builder.ToTable("PendingManualRewards", "public");
        builder.HasKey(reward => reward.Id);
        builder.Property(reward => reward.Id).ValueGeneratedOnAdd();

        builder.Property(reward => reward.RewardType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(reward => reward.CreatedAtUtc)
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(reward => reward.Status)
            .HasMaxLength(16)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.HasOne(reward => reward.User)
            .WithMany()
            .HasForeignKey(reward => reward.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(reward => reward.Status);
    }
}
