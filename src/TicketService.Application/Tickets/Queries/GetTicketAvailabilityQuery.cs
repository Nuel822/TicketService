using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;

namespace TicketService.Application.Tickets.Queries;

/// <summary>
/// Per-tier availability snapshot returned to buyers.
/// Intentionally omits TotalQuantity and SoldQuantity — those are internal
/// inventory metrics exposed only via the sales report endpoint.
/// </summary>
public record TierAvailabilityResponse(
    Guid Id,
    string Name,
    decimal Price,
    int AvailableQuantity);

/// <summary>
/// Event-level availability snapshot.
/// TotalCapacity is omitted — buyers only need to know what is available now.
/// </summary>
public record TicketAvailabilityResponse(
    Guid EventId,
    string EventName,
    int TotalAvailable,
    IReadOnlyList<TierAvailabilityResponse> Tiers);

public class GetTicketAvailabilityQuery
{
    private readonly IEventRepository _eventRepository;
    private readonly ITicketRepository _ticketRepository;

    public GetTicketAvailabilityQuery(
        IEventRepository eventRepository,
        ITicketRepository ticketRepository)
    {
        _eventRepository = eventRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<TicketAvailabilityResponse> ExecuteAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var @event = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        var tiers = await _ticketRepository.GetPricingTiersByEventIdAsync(eventId, cancellationToken);

        var tierResponses = tiers.Select(t => new TierAvailabilityResponse(
            Id:                t.Id,
            Name:              t.Name,
            Price:             t.Price,
            AvailableQuantity: t.AvailableQuantity)).ToList();

        return new TicketAvailabilityResponse(
            @event.Id,
            @event.Name,
            @event.AvailableTickets,
            tierResponses);
    }
}


