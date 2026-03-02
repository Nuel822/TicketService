using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Events.Commands;

namespace TicketService.Application.Events.Queries;

public class GetEventByIdQuery
{
    private readonly IEventRepository _eventRepository;

    public GetEventByIdQuery(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<CreateEventResponse> ExecuteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var @event = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        return CreateEventCommand.MapToResponse(@event);
    }
}


