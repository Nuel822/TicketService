namespace TicketService.Domain.Entities;

/// <summary>
/// Denormalised read model stored in the Reporting DB.
/// Represents per-tier sales data for a given event.
/// </summary>
public class TierSalesSummary
{
    public Guid PricingTierId { get; set; }
    public Guid EventId { get; set; }
    public string TierName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int TotalQuantity { get; set; }
    public int QuantitySold { get; set; }
    public int QuantityAvailable { get; set; }
    public decimal Revenue { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    // Navigation property
    public EventSalesSummary EventSalesSummary { get; set; } = null!;
}


