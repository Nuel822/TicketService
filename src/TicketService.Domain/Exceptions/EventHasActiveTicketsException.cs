namespace TicketService.Domain.Exceptions;

/// <summary>
/// Thrown when an attempt is made to delete an event that still has active ticket holders.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class EventHasActiveTicketsException : DomainException
{
    public EventHasActiveTicketsException(string eventName)
        : base($"Event '{eventName}' cannot be deleted because it has active ticket holders. Cancel all tickets before deleting the event.")
    {
    }
}


