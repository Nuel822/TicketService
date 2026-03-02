using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Text.Json;
using TicketService.Application.Common.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Domain.Enums;
using TicketService.Domain.Exceptions;
using TicketService.Infrastructure.Persistence.TicketingDb;

namespace TicketService.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TicketingDbContext _context;

    public TicketRepository(TicketingDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Include(t => t.PricingTier)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Where(t => t.EventId == eventId)
            .OrderByDescending(t => t.PurchasedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PricingTier?> GetPricingTierByIdAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        return await _context.PricingTiers
            .FirstOrDefaultAsync(t => t.Id == tierId, cancellationToken);
    }

    public async Task<IReadOnlyList<PricingTier>> GetPricingTiersByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.PricingTiers
            .Where(t => t.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasSoldTicketsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .AnyAsync(t => t.EventId == eventId && t.Status == TicketStatus.Active, cancellationToken);
    }

    /// <summary>
    /// Purchases tickets atomically in a single SQL round-trip for the tier update.
    /// This is the primary defence against overselling under concurrent load.
    ///
    /// Flow:
    /// 1. Begin transaction
    /// 2. Single UPDATE ... RETURNING on pricing_tiers:
    ///    - Locks the row (UPDATE acquires an exclusive row lock automatically)
    ///    - Checks availability via WHERE available_quantity >= $quantity
    ///    - Decrements available_quantity atomically
    ///    - Returns name and price needed for the ticket record
    ///    If 0 rows returned → tier not found or oversold (disambiguated by EXISTS check)
    /// 3. Decrement Event.AvailableTickets via EF Core
    /// 4. Insert Ticket record via EF Core
    /// 5. Insert OutboxMessage (same transaction — atomic with ticket insert)
    /// 6. Commit
    ///
    /// Note: xmin is NOT referenced in the UPDATE WHERE clause. The UPDATE row lock
    /// provides the same serialisation guarantee without the t.xmin alias issue that
    /// PostgreSQL rejects for system columns in EF Core-generated SQL.
    /// The xmin shadow property on PricingTier remains in the model for optimistic
    /// concurrency on non-purchase update paths (e.g. UpdateEventCommand).
    /// </summary>
    public async Task<Ticket> PurchaseAsync(
        Guid eventId,
        Guid pricingTierId,
        string purchaserName,
        string purchaserEmail,
        int quantity,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var conn = (NpgsqlConnection)_context.Database.GetDbConnection();

            string tierName;
            decimal tierPrice;

            // Single UPDATE ... RETURNING: locks the row, checks availability, and decrements
            // in one round-trip. No SELECT needed on the happy path.
            // xmin is not referenced here — the UPDATE lock serialises concurrent requests.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
                cmd.CommandText = @"
                    UPDATE pricing_tiers
                       SET available_quantity = available_quantity - $1
                     WHERE id = $2
                       AND available_quantity >= $1
                 RETURNING name, price";
                cmd.Parameters.AddWithValue(quantity);
                cmd.Parameters.AddWithValue(pricingTierId);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    // 0 rows: either tier doesn't exist or available_quantity < quantity.
                    // Fetch name + available_quantity in a single query to disambiguate.
                    var tierInfo = await _context.PricingTiers
                        .Where(t => t.Id == pricingTierId)
                        .Select(t => new { t.Name, t.AvailableQuantity })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (tierInfo == null)
                        throw new OversellException("Unknown", quantity, 0);

                    throw new OversellException(tierInfo.Name, quantity, tierInfo.AvailableQuantity);
                }

                tierName  = reader.GetString(0);
                tierPrice = reader.GetDecimal(1);
            }

            // Decrement Event.AvailableTickets via EF Core (Event has no xmin token)
            var @event = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Event {eventId} not found when decrementing availability. " +
                    "This indicates a data integrity issue — the event was deleted after the purchase began.");

            @event.DecrementAvailability(quantity);

            // Create the ticket
            var ticket = Ticket.Create(
                eventId,
                pricingTierId,
                purchaserName,
                purchaserEmail,
                quantity,
                tierPrice);

            await _context.Tickets.AddAsync(ticket, cancellationToken);

            // Create outbox message in the same transaction (atomic replication event)
            var payload = JsonSerializer.Serialize(new
            {
                TicketId = ticket.Id,
                EventId = eventId,
                PricingTierId = pricingTierId,
                TierName = tierName,
                Quantity = quantity,
                UnitPrice = tierPrice,
                TotalPrice = ticket.TotalPrice,
                PurchaserEmail = purchaserEmail,
                PurchasedAt = ticket.PurchasedAt
            });

            var outboxMessage = OutboxMessage.Create(
                "TicketPurchased",
                payload,
                idempotencyKey);

            await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ticket;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}


