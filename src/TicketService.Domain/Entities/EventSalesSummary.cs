namespace TicketService.Domain.Entities;

/// <summary>
/// Denormalised read model stored in the Reporting DB.
/// Populated and updated asynchronously by the OutboxProcessor.
/// </summary>
public class EventSalesSummary
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public TimeOnly EventTime { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalTicketsSold { get; set; }
    public int AvailableTickets { get; set; }
    public decimal TotalRevenue { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public ICollection<TierSalesSummary> TierSummaries { get; set; } = new List<TierSalesSummary>();
}


