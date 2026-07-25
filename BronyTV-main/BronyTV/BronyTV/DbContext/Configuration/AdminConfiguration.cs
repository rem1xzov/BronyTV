using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class AdminConfiguration : IEntityTypeConfiguration<AdminEntity>
{
    public void Configure(EntityTypeBuilder<AdminEntity> builder)
    {
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Login)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(a => a.PasswordHash)
            .IsRequired();
        
        builder.HasIndex(a => a.Login)
            .IsUnique();
    }
}