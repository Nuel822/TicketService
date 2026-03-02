using TicketService.Domain.Entities;

namespace TicketService.Application.Common.Interfaces;

public interface IReportingRepository
{

    Task<EventSalesSummary?> GetEventSalesSummaryAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<TierSalesSummary?> GetTierSalesSummaryAsync(Guid pricingTierId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<EventSalesSummary> Items, int TotalCount)> GetPagedEventSalesSummariesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task UpsertEventSalesSummaryAsync(EventSalesSummary summary, CancellationToken cancellationToken = default);

    Task UpsertTierSalesSummaryAsync(TierSalesSummary summary, CancellationToken cancellationToken = default);
}


