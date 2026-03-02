namespace TicketService.Domain.Exceptions;

/// <summary>
/// Raised when an event with the same venue, date, and time already exists.
/// </summary>
public class DuplicateEventException : DomainException
{
    public DuplicateEventException(string venue, DateOnly date, TimeOnly time)
        : base($"An event at '{venue}' on {date:yyyy-MM-dd} at {time:HH:mm} already exists.")
    {
    }
}


