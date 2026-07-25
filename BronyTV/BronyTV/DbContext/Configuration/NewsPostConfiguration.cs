using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.ToTable("NewsPosts", "public");
        builder.HasKey(news => news.Id);

        builder.Property(news => news.Title)
            .HasMaxLength(200);

        builder.Property(news => news.Content)
            .HasMaxLength(10000);

        builder.Property(news => news.ImageUrl)
            .HasMaxLength(500);

        builder.Property(news => news.AuthorUsername)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(news => news.CreatedAt)
            .IsRequired();
    }
}
