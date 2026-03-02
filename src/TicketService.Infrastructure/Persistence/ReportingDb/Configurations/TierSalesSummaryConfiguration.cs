using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketService.Domain.Entities;

namespace TicketService.Infrastructure.Persistence.ReportingDb.Configurations;

public class TierSalesSummaryConfiguration : IEntityTypeConfiguration<TierSalesSummary>
{
    public void Configure(EntityTypeBuilder<TierSalesSummary> builder)
    {
        builder.ToTable("tier_sales_summaries");

        builder.HasKey(t => t.PricingTierId);

        builder.Property(t => t.PricingTierId)
            .HasColumnName("pricing_tier_id");

        builder.Property(t => t.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(t => t.TierName)
            .HasColumnName("tier_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TotalQuantity)
            .HasColumnName("total_quantity")
            .IsRequired();

        builder.Property(t => t.QuantitySold)
            .HasColumnName("quantity_sold")
            .IsRequired();

        builder.Property(t => t.QuantityAvailable)
            .HasColumnName("quantity_available")
            .IsRequired();

        builder.Property(t => t.Revenue)
            .HasColumnName("revenue")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        builder.HasIndex(t => t.EventId)
            .HasDatabaseName("ix_tier_sales_summaries_event_id");
    }
}


