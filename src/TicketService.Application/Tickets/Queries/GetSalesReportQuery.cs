using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;

namespace TicketService.Application.Tickets.Queries;

public record TierSalesReportResponse(
    Guid TierId,
    string TierName,
    decimal UnitPrice,
    int TotalQuantity,
    int QuantitySold,
    int QuantityAvailable,
    decimal Revenue);

public record EventSalesReportResponse(
    Guid EventId,
    string EventName,
    string Venue,
    DateOnly EventDate,
    TimeOnly EventTime,
    int TotalCapacity,
    int TotalTicketsSold,
    int AvailableTickets,
    decimal TotalRevenue,
    DateTime LastUpdatedAt,
    string Note,
    IReadOnlyList<TierSalesReportResponse> SalesByTier);

/// <summary>
/// Generic pagination envelope returned by list endpoints.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class GetSalesReportQuery
{
    private readonly IReportingRepository _reportingRepository;
    private readonly IEventRepository _eventRepository;

    public GetSalesReportQuery(
        IReportingRepository reportingRepository,
        IEventRepository eventRepository)
    {
        _reportingRepository = reportingRepository;
        _eventRepository = eventRepository;
    }

    public async Task<EventSalesReportResponse> ExecuteAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        // Verify the event exists in the Write DB
        var eventExists = await _eventRepository.ExistsAsync(eventId, cancellationToken);
        if (!eventExists)
            throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        var summary = await _reportingRepository.GetEventSalesSummaryAsync(eventId, cancellationToken);

        // If no summary yet (e.g. no tickets sold), return zeroed report
        if (summary == null)
        {
            var @event = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
            return new EventSalesReportResponse(
                eventId,
                @event!.Name,
                @event.Venue,
                @event.Date,
                @event.Time,
                @event.TotalCapacity,
                0,
                @event.TotalCapacity,
                0m,
                DateTime.UtcNow,
                "No tickets have been sold for this event yet.",
                new List<TierSalesReportResponse>());
        }

        var tierReports = summary.TierSummaries.Select(t => new TierSalesReportResponse(
            t.PricingTierId,
            t.TierName,
            t.UnitPrice,
            t.TotalQuantity,
            t.QuantitySold,
            t.QuantityAvailable,
            t.Revenue)).ToList();

        return new EventSalesReportResponse(
            summary.EventId,
            summary.EventName,
            summary.Venue,
            summary.EventDate,
            summary.EventTime,
            summary.TotalCapacity,
            summary.TotalTicketsSold,
            summary.AvailableTickets,
            summary.TotalRevenue,
            summary.LastUpdatedAt,
            "Reporting data is eventually consistent and may be up to 5 seconds behind live data.",
            tierReports);
    }

    /// <summary>
    /// Returns a paginated list of sales reports for all events.
    /// Page is 1-based. PageSize is clamped between 1 and 100.
    /// Pagination is pushed to the database — only the requested page is loaded into memory.
    /// </summary>
    public async Task<PagedResult<EventSalesReportResponse>> ExecuteAllAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Clamp inputs to safe bounds
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var skip = (page - 1) * pageSize;

        var (summaries, totalCount) = await _reportingRepository
            .GetPagedEventSalesSummariesAsync(skip, pageSize, cancellationToken);

        var pagedItems = summaries.Select(summary =>
        {
            var tierReports = summary.TierSummaries.Select(t => new TierSalesReportResponse(
                t.PricingTierId,
                t.TierName,
                t.UnitPrice,
                t.TotalQuantity,
                t.QuantitySold,
                t.QuantityAvailable,
                t.Revenue)).ToList();

            return new EventSalesReportResponse(
                summary.EventId,
                summary.EventName,
                summary.Venue,
                summary.EventDate,
                summary.EventTime,
                summary.TotalCapacity,
                summary.TotalTicketsSold,
                summary.AvailableTickets,
                summary.TotalRevenue,
                summary.LastUpdatedAt,
                "Reporting data is eventually consistent and may be up to 5 seconds behind live data.",
                tierReports);
        }).ToList();

        return new PagedResult<EventSalesReportResponse>(pagedItems, page, pageSize, totalCount);
    }
}


