using TicketService.Domain.Entities;

namespace TicketService.Application.Common.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Event> AddAsync(Event @event, CancellationToken cancellationToken = default);
    Task UpdateAsync(Event @event, CancellationToken cancellationToken = default);
    Task DeleteAsync(Event @event, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);


    Task<bool> ExistsAtVenueAndDateTimeAsync(
        string venue,
        DateOnly date,
        TimeOnly time,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}


