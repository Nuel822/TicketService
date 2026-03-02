using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketService.Domain.Entities;

namespace TicketService.Infrastructure.Persistence.TicketingDb.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(t => t.PricingTierId)
            .HasColumnName("pricing_tier_id")
            .IsRequired();

        builder.Property(t => t.PurchaserName)
            .HasColumnName("purchaser_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.PurchaserEmail)
            .HasColumnName("purchaser_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(t => t.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(t => t.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TotalPrice)
            .HasColumnName("total_price")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.PurchasedAt)
            .HasColumnName("purchased_at")
            .IsRequired();

        builder.Property(t => t.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(t => t.RefundedAt)
            .HasColumnName("refunded_at");

        builder.HasIndex(t => t.EventId)
            .HasDatabaseName("ix_tickets_event_id");

        builder.HasIndex(t => t.PurchaserEmail)
            .HasDatabaseName("ix_tickets_purchaser_email");

        builder.HasIndex(t => new { t.EventId, t.Status })
            .HasDatabaseName("ix_tickets_event_id_status");
    }
}


