using Microsoft.EntityFrameworkCore;
using TicketService.Application.Common.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Infrastructure.Persistence.TicketingDb;

namespace TicketService.Infrastructure.Repositories;

public class EventRepository : IEventRepository
{
    private readonly TicketingDbContext _context;

    public EventRepository(TicketingDbContext context)
    {
        _context = context;
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Events
            .Include(e => e.PricingTiers)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Events
            .Include(e => e.PricingTiers)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Time);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Event> AddAsync(Event @event, CancellationToken cancellationToken = default)
    {
        await _context.Events.AddAsync(@event, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return @event;
    }

    public async Task UpdateAsync(Event @event, CancellationToken cancellationToken = default)
    {
        _context.Events.Update(@event);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Event @event, CancellationToken cancellationToken = default)
    {
        _context.Events.Remove(@event);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Events.AnyAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAtVenueAndDateTimeAsync(
        string venue,
        DateOnly date,
        TimeOnly time,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.Events.AnyAsync(
            e => e.Venue == venue &&
                 e.Date == date &&
                 e.Time == time &&
                 (excludeId == null || e.Id != excludeId.Value),
            cancellationToken);
    }
}


