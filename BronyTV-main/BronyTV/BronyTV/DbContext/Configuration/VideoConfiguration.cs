using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class VideoConfiguration : IEntityTypeConfiguration<VideoEntity>
{
    public void Configure(EntityTypeBuilder<VideoEntity> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(v => v.Title)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(v => v.FilePath)
            .IsRequired();
        builder.Property(v => v.PreviewImageUrl);
    }
}