namespace TicketService.Domain.Exceptions;

/// <summary>
/// Base class for all domain rule violation exceptions.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}


