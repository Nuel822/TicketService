using TicketService.Domain.Exceptions;

namespace TicketService.Domain.Entities;

public class Event
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Venue { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public int TotalCapacity { get; private set; }
    public int AvailableTickets { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public ICollection<PricingTier> PricingTiers { get; private set; } = new List<PricingTier>();
    public ICollection<Ticket> Tickets { get; private set; } = new List<Ticket>();

    // EF Core requires a parameterless constructor
    private Event() { }

    public static Event Create(
        string name,
        string description,
        string venue,
        DateOnly date,
        TimeOnly time,
        int totalCapacity)
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Venue = venue,
            Date = date,
            Time = time,
            TotalCapacity = totalCapacity,
            AvailableTickets = totalCapacity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string description,
        string venue,
        DateOnly date,
        TimeOnly time,
        int totalCapacity)
    {
        var soldTickets = TotalCapacity - AvailableTickets;

        if (totalCapacity < soldTickets)
            throw new OversellException(
                "Event",
                soldTickets,
                totalCapacity);

        Name = name;
        Description = description;
        Venue = venue;
        Date = date;
        Time = time;
        TotalCapacity = totalCapacity;
        AvailableTickets = totalCapacity - soldTickets;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecrementAvailability(int quantity)
    {
        if (AvailableTickets < quantity)
            throw new OversellException("Event", quantity, AvailableTickets);

        AvailableTickets -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementAvailability(int quantity)
    {
        if (AvailableTickets + quantity > TotalCapacity)
            throw new OversellException(
                "Event",
                AvailableTickets + quantity,
                TotalCapacity);

        AvailableTickets += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Recalculates AvailableTickets as the sum of all pricing tier available quantities.
    /// Must be called after any tier update to keep the event-level counter in sync.
    /// </summary>
    public void RecalculateAvailability()
    {
        AvailableTickets = PricingTiers.Sum(t => t.AvailableQuantity);
        UpdatedAt = DateTime.UtcNow;
    }
}


