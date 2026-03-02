using TicketService.Domain.Enums;
using TicketService.Domain.Exceptions;

namespace TicketService.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid PricingTierId { get; private set; }
    public string PurchaserName { get; private set; } = string.Empty;
    public string PurchaserEmail { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTime PurchasedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    // Navigation properties
    public Event Event { get; private set; } = null!;
    public PricingTier PricingTier { get; private set; } = null!;

    // EF Core requires a parameterless constructor
    private Ticket() { }

    public static Ticket Create(
        Guid eventId,
        Guid pricingTierId,
        string purchaserName,
        string purchaserEmail,
        int quantity,
        decimal unitPrice)
    {
        return new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            PricingTierId = pricingTierId,
            PurchaserName = purchaserName,
            PurchaserEmail = purchaserEmail,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = unitPrice * quantity,
            Status = TicketStatus.Active,
            PurchasedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Cancels the ticket. Throws <see cref="InvalidTicketStateException"/> if already cancelled or refunded.
    /// Reserved for a future cancellation endpoint (POST /api/tickets/{id}/cancel).
    /// </summary>
    public void Cancel()
    {
        if (Status == TicketStatus.Cancelled)
            throw new InvalidTicketStateException("Ticket is already cancelled.");

        if (Status == TicketStatus.Refunded)
            throw new InvalidTicketStateException("Ticket has already been refunded and cannot be cancelled.");

        Status = TicketStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the ticket as refunded. Throws <see cref="InvalidTicketStateException"/> if already refunded.
    /// Reserved for a future refund endpoint (POST /api/tickets/{id}/refund).
    /// </summary>
    public void Refund()
    {
        if (Status == TicketStatus.Refunded)
            throw new InvalidTicketStateException("Ticket has already been refunded.");

        Status = TicketStatus.Refunded;
        RefundedAt = DateTime.UtcNow;
    }
}


