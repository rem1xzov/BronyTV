using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("Users", "public");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.Username)
            .HasMaxLength(15)
            .IsRequired(false);

        builder.HasIndex(user => user.Username)
            .IsUnique()
            .HasFilter("\"Username\" IS NOT NULL");

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Race)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.Property(user => user.RaceSelectedAtUtc)
            .IsRequired();
    }
}
