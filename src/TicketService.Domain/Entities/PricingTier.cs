using TicketService.Domain.Exceptions;

namespace TicketService.Domain.Entities;

public class PricingTier
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int TotalQuantity { get; private set; }
    public int AvailableQuantity { get; private set; }

    // Navigation properties
    public Event Event { get; private set; } = null!;
    public ICollection<Ticket> Tickets { get; private set; } = new List<Ticket>();

    // EF Core requires a parameterless constructor
    private PricingTier() { }

    public static PricingTier Create(Guid eventId, string name, decimal price, int quantity)
    {
        return new PricingTier
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = name,
            Price = price,
            TotalQuantity = quantity,
            AvailableQuantity = quantity
        };
    }

    public void Update(string name, decimal price, int totalQuantity)
    {
        var sold = TotalQuantity - AvailableQuantity;

        if (totalQuantity < sold)
            throw new OversellException(
                Name,
                sold,
                totalQuantity);

        Name = name;
        Price = price;
        TotalQuantity = totalQuantity;
        AvailableQuantity = totalQuantity - sold;
    }

    public void DecrementAvailability(int quantity)
    {
        if (AvailableQuantity < quantity)
            throw new OversellException(Name, quantity, AvailableQuantity);

        AvailableQuantity -= quantity;
    }

    /// <summary>
    /// Restores availability after a cancellation or refund.
    /// Reserved for a future cancellation/refund endpoint.
    /// </summary>
    public void IncrementAvailability(int quantity)
    {
        AvailableQuantity += quantity;
    }

    /// <summary>
    /// Returns true if at least <paramref name="quantity"/> seats are still available
    /// according to the in-memory snapshot loaded from the DB.
    /// Used as an optimistic fast-fail pre-check in <c>PurchaseTicketCommand</c> to
    /// reject obviously-impossible requests before opening a transaction.
    /// The authoritative oversell guard is the pessimistic SQL lock inside
    /// <c>TicketRepository.PurchaseAsync</c> — this check does not replace it.
    /// </summary>
    public bool HasAvailability(int quantity) => AvailableQuantity >= quantity;
}


