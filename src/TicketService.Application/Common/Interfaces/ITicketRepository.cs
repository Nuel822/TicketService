using TicketService.Domain.Entities;

namespace TicketService.Application.Common.Interfaces;

public interface ITicketRepository
{
    /// <summary>
    /// Retrieves a single ticket by its ID.
    /// Reserved for a future ticket-detail or cancellation endpoint (GET /api/tickets/{id}).
    /// </summary>
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tickets for a given event.
    /// Reserved for a future event-tickets listing endpoint (GET /api/events/{eventId}/tickets).
    /// </summary>
    Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the event has any tickets that are not cancelled or refunded.
    /// Used to guard against deleting events with active ticket holders.
    /// </summary>
    Task<bool> HasSoldTicketsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purchases tickets atomically using a pessimistic lock on the PricingTier row.
    /// Decrements availability on both PricingTier and Event within a single transaction.
    /// </summary>
    Task<Ticket> PurchaseAsync(
        Guid eventId,
        Guid pricingTierId,
        string purchaserName,
        string purchaserEmail,
        int quantity,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PricingTier?> GetPricingTierByIdAsync(Guid tierId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PricingTier>> GetPricingTiersByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}


