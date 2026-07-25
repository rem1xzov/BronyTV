using BronyTV.DbContext.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BronyTV.DbContext.Configuration;

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessageEntity>
{
    public void Configure(EntityTypeBuilder<SupportMessageEntity> builder)
    {
        builder.ToTable("SupportMessages", "public");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Content)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(message => message.TicketId);
        builder.HasIndex(message => message.CreatedAtUtc);

        builder.HasOne(message => message.Ticket)
            .WithMany(ticket => ticket.Messages)
            .HasForeignKey(message => message.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
