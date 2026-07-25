using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicketEntity>
{
    public void Configure(EntityTypeBuilder<SupportTicketEntity> builder)
    {
        builder.ToTable("SupportTickets", "public");
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(ticket => ticket.IsClosed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(ticket => ticket.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(ticket => ticket.UserId);
        builder.HasIndex(ticket => ticket.CreatedAtUtc);
        builder.HasIndex(ticket => ticket.IsClosed);

        builder.HasOne(ticket => ticket.User)
            .WithMany()
            .HasForeignKey(ticket => ticket.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
