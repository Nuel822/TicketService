using FluentValidation;
using TicketService.API.Filters;
using TicketService.Application.Events.Commands;
using TicketService.Application.Events.Queries;
using TicketService.Application.Tickets.Queries;

namespace TicketService.API.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/events?page=1&pageSize=20
        group.MapGet("/", async (
            GetAllEventsQuery query,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var events = await query.ExecuteAsync(page, pageSize, ct);
            return Results.Ok(events);
        })
        .WithName("GetAllEvents")
        .WithSummary("Get all events (paginated, ordered by date)")
        .RequireRateLimiting("reads")
        .Produces<PagedResult<CreateEventResponse>>(StatusCodes.Status200OK);

        // GET /api/events/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            GetEventByIdQuery query,
            CancellationToken ct) =>
        {
            var @event = await query.ExecuteAsync(id, ct);
            return Results.Ok(@event);
        })
        .WithName("GetEventById")
        .WithSummary("Get a single event by ID")
        .RequireRateLimiting("reads")
        .Produces<CreateEventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/events
        group.MapPost("/", async (
            CreateEventRequest request,
            CreateEventCommand command,
            CancellationToken ct) =>
        {
            var result = await command.ExecuteAsync(request, ct);
            return Results.CreatedAtRoute("GetEventById", new { id = result.Id }, result);
        })
        .WithName("CreateEvent")
        .WithSummary("Create a new event")
        .RequireRateLimiting("writes")
        .AddEndpointFilter<ValidationFilter<CreateEventRequest>>()
        .Produces<CreateEventResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // PUT /api/events/{id}
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEventRequest request,
            UpdateEventCommand command,
            CancellationToken ct) =>
        {
            var result = await command.ExecuteAsync(id, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateEvent")
        .WithSummary("Update an existing event")
        .RequireRateLimiting("writes")
        .AddEndpointFilter<ValidationFilter<UpdateEventRequest>>()
        .Produces<CreateEventResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // DELETE /api/events/{id}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            DeleteEventCommand command,
            CancellationToken ct) =>
        {
            await command.ExecuteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteEvent")
        .WithSummary("Delete an event")
        .RequireRateLimiting("writes")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}


