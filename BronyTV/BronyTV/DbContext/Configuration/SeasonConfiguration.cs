using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class SeasonConfiguration : IEntityTypeConfiguration<SeasonEntity>
{
    public void Configure(EntityTypeBuilder<SeasonEntity> builder)
    {
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Number)
            .IsRequired();
        builder.Property(s => s.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(s => s.Description)
            .HasMaxLength(1000);
        builder.Property(s => s.PosterPath)
            .IsRequired();
        
        builder.HasMany(s => s.Videos)
            .WithOne(v => v.Season)
            .HasForeignKey(v => v.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}