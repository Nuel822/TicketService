using TicketService.Application.Common.Interfaces;
using TicketService.Application.Events.Commands;
using TicketService.Application.Tickets.Queries;

namespace TicketService.Application.Events.Queries;

public class GetAllEventsQuery
{
    private readonly IEventRepository _eventRepository;

    public GetAllEventsQuery(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    /// <summary>
    /// Returns a paginated list of events ordered by date then time.
    /// Page is 1-based. PageSize is clamped between 1 and 100.
    /// Pagination is pushed to the database — only the requested page is loaded into memory.
    /// </summary>
    public async Task<PagedResult<CreateEventResponse>> ExecuteAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var skip = (page - 1) * pageSize;

        var (events, totalCount) = await _eventRepository.GetPagedAsync(skip, pageSize, cancellationToken);

        var items = events.Select(CreateEventCommand.MapToResponse).ToList();

        return new PagedResult<CreateEventResponse>(items, page, pageSize, totalCount);
    }
}


