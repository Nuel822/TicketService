using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;
using TicketService.Domain.Exceptions;

namespace TicketService.Application.Events.Commands;

public class DeleteEventCommand
{
    private readonly IEventRepository _eventRepository;
    private readonly ITicketRepository _ticketRepository;

    public DeleteEventCommand(IEventRepository eventRepository, ITicketRepository ticketRepository)
    {
        _eventRepository = eventRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task ExecuteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var @event = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        var hasSoldTickets = await _ticketRepository.HasSoldTicketsAsync(eventId, cancellationToken);
        if (hasSoldTickets)
            throw new EventHasActiveTicketsException(@event.Name);

        await _eventRepository.DeleteAsync(@event, cancellationToken);
    }
}


