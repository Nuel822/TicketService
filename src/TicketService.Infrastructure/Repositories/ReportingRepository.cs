using Microsoft.EntityFrameworkCore;
using TicketService.Application.Common.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Infrastructure.Persistence.ReportingDb;

namespace TicketService.Infrastructure.Repositories;

public class ReportingRepository : IReportingRepository
{
    private readonly ReportingDbContext _context;

    public ReportingRepository(ReportingDbContext context)
    {
        _context = context;
    }

    public async Task<EventSalesSummary?> GetEventSalesSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EventSalesSummaries
            .Include(s => s.TierSummaries)
            .FirstOrDefaultAsync(s => s.EventId == eventId, cancellationToken);
    }

    public async Task<TierSalesSummary?> GetTierSalesSummaryAsync(
        Guid pricingTierId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TierSalesSummaries
            .FirstOrDefaultAsync(t => t.PricingTierId == pricingTierId, cancellationToken);
    }

    public async Task<(IReadOnlyList<EventSalesSummary> Items, int TotalCount)> GetPagedEventSalesSummariesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EventSalesSummaries
            .Include(s => s.TierSummaries)
            .OrderBy(s => s.EventName);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Upserts an EventSalesSummary into the Reporting DB.
    /// If a row with the same EventId already exists, it is updated in place.
    /// If not, a new row is inserted.
    /// EF Core tracks the entity state to decide INSERT vs UPDATE.
    /// </summary>
    public async Task UpsertEventSalesSummaryAsync(
        EventSalesSummary summary,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.EventSalesSummaries
            .FirstOrDefaultAsync(s => s.EventId == summary.EventId, cancellationToken);

        if (existing == null)
        {
            await _context.EventSalesSummaries.AddAsync(summary, cancellationToken);
        }
        else
        {
            // Copy scalar fields onto the tracked entity so EF generates an UPDATE
            existing.EventName = summary.EventName;
            existing.Venue = summary.Venue;
            existing.EventDate = summary.EventDate;
            existing.EventTime = summary.EventTime;
            existing.TotalCapacity = summary.TotalCapacity;
            existing.TotalTicketsSold = summary.TotalTicketsSold;
            existing.AvailableTickets = summary.AvailableTickets;
            existing.TotalRevenue = summary.TotalRevenue;
            existing.LastUpdatedAt = summary.LastUpdatedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Upserts a TierSalesSummary into the Reporting DB.
    /// If a row with the same PricingTierId already exists, it is updated in place.
    /// If not, a new row is inserted.
    /// </summary>
    public async Task UpsertTierSalesSummaryAsync(
        TierSalesSummary summary,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.TierSalesSummaries
            .FirstOrDefaultAsync(t => t.PricingTierId == summary.PricingTierId, cancellationToken);

        if (existing == null)
        {
            await _context.TierSalesSummaries.AddAsync(summary, cancellationToken);
        }
        else
        {
            existing.TierName = summary.TierName;
            existing.UnitPrice = summary.UnitPrice;
            existing.TotalQuantity = summary.TotalQuantity;
            existing.QuantitySold = summary.QuantitySold;
            existing.QuantityAvailable = summary.QuantityAvailable;
            existing.Revenue = summary.Revenue;
            existing.LastUpdatedAt = summary.LastUpdatedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}


