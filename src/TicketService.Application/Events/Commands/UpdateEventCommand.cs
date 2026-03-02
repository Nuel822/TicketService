using System.Text.Json.Serialization;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;
using TicketService.Domain.Exceptions;

namespace TicketService.Application.Events.Commands;

public record UpdateEventRequest(
    string Name,
    string Description,
    string Venue,
    DateOnly Date,
    TimeOnly Time,
    int TotalCapacity,
    IReadOnlyList<UpdatePricingTierRequest> PricingTiers);


public record UpdatePricingTierRequest(
    [property: JsonPropertyName("existingTierId")] 
    Guid? ExistingTierId,
    string Name,
    decimal Price,
    int Quantity);

public class UpdateEventCommand
{
    private readonly IEventRepository _eventRepository;

    public UpdateEventCommand(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<CreateEventResponse> ExecuteAsync(
        Guid eventId,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var @event = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        var isDuplicate = await _eventRepository.ExistsAtVenueAndDateTimeAsync(
            request.Venue, request.Date, request.Time,
            excludeId: eventId,
            cancellationToken: cancellationToken);

        if (isDuplicate)
            throw new DuplicateEventException(request.Venue, request.Date, request.Time);

        @event.Update(
            request.Name,
            request.Description,
            request.Venue,
            request.Date,
            request.Time,
            request.TotalCapacity);

        foreach (var tierRequest in request.PricingTiers)
        {
            if (tierRequest.ExistingTierId.HasValue)
            {
                var existingTier = @event.PricingTiers.FirstOrDefault(t => t.Id == tierRequest.ExistingTierId.Value);
                existingTier?.Update(tierRequest.Name, tierRequest.Price, tierRequest.Quantity);
            }
            else
            {
                var newTier = Domain.Entities.PricingTier.Create(
                    @event.Id,
                    tierRequest.Name,
                    tierRequest.Price,
                    tierRequest.Quantity);
                @event.PricingTiers.Add(newTier);
            }
        }

        // Resync the event-level available ticket counter from the sum of tier available quantities
        @event.RecalculateAvailability();

        await _eventRepository.UpdateAsync(@event, cancellationToken);

        return CreateEventCommand.MapToResponse(@event);
    }
}


