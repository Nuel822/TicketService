namespace TicketService.Domain.Exceptions;

/// <summary>
/// Thrown when a ticket state transition is invalid
/// (e.g. cancelling an already-cancelled or refunded ticket).
/// Maps to HTTP 409 Conflict.
/// </summary>
public class InvalidTicketStateException : DomainException
{
    public InvalidTicketStateException(string message)
        : base(message)
    {
    }
}


