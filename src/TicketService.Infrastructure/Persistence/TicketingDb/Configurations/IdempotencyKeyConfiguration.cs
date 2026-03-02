using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketService.Domain.Entities;

namespace TicketService.Infrastructure.Persistence.TicketingDb.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(k => k.Key);

        builder.Property(k => k.Key)
            .HasColumnName("key")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(k => k.RequestPath)
            .HasColumnName("request_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(k => k.ResponseStatusCode)
            .HasColumnName("response_status_code")
            .IsRequired();

        builder.Property(k => k.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        // Index for efficient expiry cleanup
        builder.HasIndex(k => k.ExpiresAt)
            .HasDatabaseName("ix_idempotency_keys_expires_at");
    }
}


