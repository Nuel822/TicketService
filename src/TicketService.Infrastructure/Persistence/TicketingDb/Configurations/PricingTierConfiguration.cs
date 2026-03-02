using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketService.Domain.Entities;

namespace TicketService.Infrastructure.Persistence.TicketingDb.Configurations;

public class PricingTierConfiguration : IEntityTypeConfiguration<PricingTier>
{
    public void Configure(EntityTypeBuilder<PricingTier> builder)
    {
        builder.ToTable("pricing_tiers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Price)
            .HasColumnName("price")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TotalQuantity)
            .HasColumnName("total_quantity")
            .IsRequired();

        builder.Property(t => t.AvailableQuantity)
            .HasColumnName("available_quantity")
            .IsRequired();

        // PostgreSQL xmin is a system column that changes on every row update.
        // Npgsql's NpgsqlPostgresModelFinalizingConvention automatically maps a uint shadow
        // property named "xmin" with ValueGeneratedOnAddOrUpdate + IsConcurrencyToken to the
        // PostgreSQL xmin system column — no DDL column is created in migrations.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(t => t.Tickets)
            .WithOne(tk => tk.PricingTier)
            .HasForeignKey(tk => tk.PricingTierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.EventId)
            .HasDatabaseName("ix_pricing_tiers_event_id");
    }
}


