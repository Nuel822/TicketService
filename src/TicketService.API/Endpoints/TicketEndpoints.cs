using TicketService.API.Filters;
using TicketService.Application.Tickets.Commands;
using TicketService.Application.Tickets.Queries;

namespace TicketService.API.Endpoints;

public static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/events/{eventId}/tickets/availability
        group.MapGet("/{eventId:guid}/tickets/availability", async (
            Guid eventId,
            GetTicketAvailabilityQuery query,
            CancellationToken ct) =>
        {
            var availability = await query.ExecuteAsync(eventId, ct);
            return Results.Ok(availability);
        })
        .WithName("GetTicketAvailability")
        .WithSummary("Get ticket availability for an event")
        .RequireRateLimiting("reads")
        .Produces<TicketAvailabilityResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/events/{eventId}/tickets
        // Idempotency-Key header is optional but strongly recommended for purchase requests
        // to prevent duplicate charges on network retries.
        //
        // Behaviour:
        //   • First request with a given key  → executes purchase, returns 201 Created
        //   • Retry with the same key (≤24 h) → returns cached response, 200 OK
        //     (RFC 7231 §6.3.1: 200 is correct for a replayed safe response; the resource
        //      was already created, so 201 would be misleading on a replay)
        group.MapPost("/{eventId:guid}/tickets", async (
            Guid eventId,
            PurchaseTicketRequest request,
            PurchaseTicketCommand command,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // Extract optional idempotency key from request header
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

            var result = await command.ExecuteAsync(eventId, request, idempotencyKey, ct);

            // Replay: resource already exists — return 200 OK with the original response body.
            if (result.IsReplay)
                return Results.Ok(result.Response);

            // Fresh purchase: 201 Created with a Location header pointing to the ticket
            // availability endpoint. There is no GET /tickets/{id} route, so we point to
            // the event's availability resource — the canonical URL that reflects the
            // updated inventory state.
            return Results.Created(
                $"/api/events/{eventId}/tickets/availability",
                result.Response);
        })
        .WithName("PurchaseTicket")
        .WithSummary("Purchase tickets for an event")
        .WithDescription(
            "Purchases tickets for an event. Supply an `Idempotency-Key` header (UUID recommended) " +
            "to make the request safe to retry. A repeated request with the same key within 24 hours " +
            "returns the original response (HTTP 200) without executing a second purchase.")
        .RequireRateLimiting("purchases")
        .AddEndpointFilter<ValidationFilter<PurchaseTicketRequest>>()
        .Produces<PurchaseTicketResponse>(StatusCodes.Status201Created)
        .Produces<PurchaseTicketResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/reports/events/{eventId}/sales
        group.MapGet("/events/{eventId:guid}/sales", async (
            Guid eventId,
            GetSalesReportQuery query,
            CancellationToken ct) =>
        {
            var report = await query.ExecuteAsync(eventId, ct);
            return Results.Ok(report);
        })
        .WithName("GetEventSalesReport")
        .WithSummary("Get ticket sales summary for a specific event (eventually consistent)")
        .RequireRateLimiting("reads")
        .Produces<EventSalesReportResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/reports/events/sales?page=1&pageSize=20
        // Returns a paginated list of sales summaries for all events.
        // page     : 1-based page number (default: 1)
        // pageSize : items per page, clamped to 1–100 (default: 20)
        group.MapGet("/events/sales", async (
            GetSalesReportQuery query,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var reports = await query.ExecuteAllAsync(page, pageSize, ct);
            return Results.Ok(reports);
        })
        .WithName("GetAllEventSalesReports")
        .WithSummary("Get paginated ticket sales summaries for all events (eventually consistent)")
        .RequireRateLimiting("reads")
        .Produces<PagedResult<EventSalesReportResponse>>(StatusCodes.Status200OK);

        return group;
    }
}


