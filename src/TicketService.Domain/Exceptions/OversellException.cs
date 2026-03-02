namespace TicketService.Domain.Exceptions;

/// <summary>
/// Thrown when a ticket purchase would exceed available inventory.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class OversellException : DomainException
{
    public string TierName { get; }
    public int Requested { get; }
    public int Available { get; }

    public OversellException(string tierName, int requested, int available)
        : base($"Cannot purchase {requested} ticket(s) for tier '{tierName}'. Only {available} ticket(s) available.")
    {
        TierName = tierName;
        Requested = requested;
        Available = available;
    }
}


