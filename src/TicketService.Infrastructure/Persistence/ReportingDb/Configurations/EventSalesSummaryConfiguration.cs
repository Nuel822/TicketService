using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketService.Domain.Entities;

namespace TicketService.Infrastructure.Persistence.ReportingDb.Configurations;

public class EventSalesSummaryConfiguration : IEntityTypeConfiguration<EventSalesSummary>
{
    public void Configure(EntityTypeBuilder<EventSalesSummary> builder)
    {
        builder.ToTable("event_sales_summaries");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId)
            .HasColumnName("event_id");

        builder.Property(e => e.EventName)
            .HasColumnName("event_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Venue)
            .HasColumnName("venue")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.EventDate)
            .HasColumnName("event_date")
            .IsRequired();

        builder.Property(e => e.EventTime)
            .HasColumnName("event_time")
            .IsRequired();

        builder.Property(e => e.TotalCapacity)
            .HasColumnName("total_capacity")
            .IsRequired();

        builder.Property(e => e.TotalTicketsSold)
            .HasColumnName("total_tickets_sold")
            .IsRequired();

        builder.Property(e => e.AvailableTickets)
            .HasColumnName("available_tickets")
            .IsRequired();

        builder.Property(e => e.TotalRevenue)
            .HasColumnName("total_revenue")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(e => e.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        builder.HasMany(e => e.TierSummaries)
            .WithOne(t => t.EventSalesSummary)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


