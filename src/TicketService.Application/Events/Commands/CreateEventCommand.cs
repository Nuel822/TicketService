using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;
using TicketService.Application.Common.Interfaces;

namespace TicketService.Application.Events.Commands;

public record CreatePricingTierRequest(
    string Name,
    decimal Price,
    int Quantity);

public record CreateEventRequest(
    string Name,
    string Description,
    string Venue,
    DateOnly Date,
    TimeOnly Time,
    int TotalCapacity,
    IReadOnlyList<CreatePricingTierRequest> PricingTiers);

public record CreateEventResponse(
    Guid Id,
    string Name,
    string Description,
    string Venue,
    DateOnly Date,
    TimeOnly Time,
    int TotalCapacity,
    int AvailableTickets,
    IReadOnlyList<PricingTierResponse> PricingTiers,
    DateTime CreatedAt);

public record PricingTierResponse(
    Guid Id,
    string Name,
    decimal Price,
    int TotalQuantity,
    int AvailableQuantity);

public class CreateEventCommand
{
    private readonly IEventRepository _eventRepository;

    public CreateEventCommand(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<CreateEventResponse> ExecuteAsync(
        CreateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var isDuplicate = await _eventRepository.ExistsAtVenueAndDateTimeAsync(
            request.Venue, request.Date, request.Time, cancellationToken: cancellationToken);

        if (isDuplicate)
            throw new DuplicateEventException(request.Venue, request.Date, request.Time);

        var @event = Event.Create(
            request.Name,
            request.Description,
            request.Venue,
            request.Date,
            request.Time,
            request.TotalCapacity);

        foreach (var tierRequest in request.PricingTiers)
        {
            var tier = PricingTier.Create(
                @event.Id,
                tierRequest.Name,
                tierRequest.Price,
                tierRequest.Quantity);

            @event.PricingTiers.Add(tier);
        }

        await _eventRepository.AddAsync(@event, cancellationToken);

        return MapToResponse(@event);
    }

    public static CreateEventResponse MapToResponse(Event @event) =>
        new(
            @event.Id,
            @event.Name,
            @event.Description,
            @event.Venue,
            @event.Date,
            @event.Time,
            @event.TotalCapacity,
            @event.AvailableTickets,
            @event.PricingTiers.Select(t => new PricingTierResponse(
                t.Id, t.Name, t.Price, t.TotalQuantity, t.AvailableQuantity)).ToList(),
            @event.CreatedAt);
}


