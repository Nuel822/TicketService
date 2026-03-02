using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TicketService.Application.Common.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Infrastructure.Persistence.TicketingDb;

namespace TicketService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that polls the outbox_messages table every 5 seconds
/// and applies unprocessed events to the Reporting DB via IReportingRepository.
///
/// Why IServiceScopeFactory?
/// IHostedService is registered as a singleton, but DbContext is scoped.
/// We cannot inject scoped services directly into a singleton — doing so
/// would cause the DbContext to live for the lifetime of the application
/// (a "captive dependency" bug). Instead, we create a new DI scope per
/// processing cycle so each batch gets a fresh DbContext and repository.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    // Polling intervals: short when messages are found, longer when idle
    private static readonly TimeSpan ActivePollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdlePollingInterval   = TimeSpan.FromSeconds(30);

    // Maximum retries before a message is considered dead-lettered
    private const int MaxRetries = 5;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            bool hadMessages = false;
            try
            {
                hadMessages = await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log but don't crash the background service — it will retry on next cycle
                _logger.LogError(ex, "Unexpected error in OutboxProcessor cycle.");
            }

            // Poll frequently while there are messages to drain; back off when idle
            var delay = hadMessages ? ActivePollingInterval : IdlePollingInterval;
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task<bool> ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        // Create a new DI scope for this processing cycle so DbContext and repositories
        // are fresh (avoids captive-dependency issues with the singleton host service).
        await using var scope = _scopeFactory.CreateAsyncScope();

        var ticketingDb         = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
        var reportingRepository = scope.ServiceProvider.GetRequiredService<IReportingRepository>();

        // Fetch unprocessed messages that haven't exceeded the retry limit
        var messages = await ticketingDb.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(50) // Process in batches of 50 to avoid long-running transactions
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return false;

        _logger.LogDebug("OutboxProcessor: processing {Count} message(s).", messages.Count);

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, ticketingDb, reportingRepository, cancellationToken);
        }

        return true;
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        TicketingDbContext ticketingDb,
        IReportingRepository reportingRepository,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (message.EventType)
            {
                case "TicketPurchased":
                    await HandleTicketPurchasedAsync(message, ticketingDb, reportingRepository, cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "OutboxProcessor: unknown event type '{EventType}' for message {MessageId}. Skipping.",
                        message.EventType, message.Id);
                    break;
            }

            message.MarkProcessed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "OutboxProcessor: failed to process message {MessageId} (attempt {Attempt}).",
                message.Id, message.RetryCount + 1);

            message.MarkFailed(ex.Message);

            if (message.IsDeadLettered(MaxRetries))
            {
                _logger.LogCritical(
                    "OutboxProcessor: message {MessageId} has been dead-lettered after {MaxRetries} retries.",
                    message.Id, MaxRetries);
            }
        }
        finally
        {
            // Always persist the updated ProcessedAt / RetryCount / Error back to the Ticketing DB
            await ticketingDb.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleTicketPurchasedAsync(
        OutboxMessage message,
        TicketingDbContext ticketingDb,
        IReportingRepository reportingRepository,
        CancellationToken cancellationToken)
    {
        // Deserialize the payload written by TicketRepository.PurchaseAsync
        var payload = JsonSerializer.Deserialize<TicketPurchasedPayload>(
            message.Payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload == null)
            throw new InvalidOperationException($"Failed to deserialize TicketPurchased payload for message {message.Id}.");

        // Load the Event from the Ticketing DB to get venue/date/time/capacity data
        var @event = await ticketingDb.Events
            .Include(e => e.PricingTiers)
            .FirstOrDefaultAsync(e => e.Id == payload.EventId, cancellationToken);

        if (@event == null)
            throw new InvalidOperationException($"Event {payload.EventId} not found when processing outbox message {message.Id}.");

        var tier = @event.PricingTiers.FirstOrDefault(t => t.Id == payload.PricingTierId);

        if (tier == null)
            throw new InvalidOperationException($"PricingTier {payload.PricingTierId} not found when processing outbox message {message.Id}.");

        // ── Upsert EventSalesSummary via IReportingRepository ─────────────────
        // Fetch the existing summary (if any) so we can increment counters correctly.
        var existingEventSummary = await reportingRepository
            .GetEventSalesSummaryAsync(payload.EventId, cancellationToken);

        if (existingEventSummary == null)
        {
            var newSummary = new EventSalesSummary
            {
                EventId          = @event.Id,
                EventName        = @event.Name,
                Venue            = @event.Venue,
                EventDate        = @event.Date,
                EventTime        = @event.Time,
                TotalCapacity    = @event.TotalCapacity,
                TotalTicketsSold = payload.Quantity,
                AvailableTickets = @event.AvailableTickets,
                TotalRevenue     = payload.TotalPrice,
                LastUpdatedAt    = DateTime.UtcNow
            };

            await reportingRepository.UpsertEventSalesSummaryAsync(newSummary, cancellationToken);
        }
        else
        {
            existingEventSummary.TotalTicketsSold += payload.Quantity;
            existingEventSummary.AvailableTickets  = @event.AvailableTickets;
            existingEventSummary.TotalRevenue     += payload.TotalPrice;
            existingEventSummary.LastUpdatedAt     = DateTime.UtcNow;

            await reportingRepository.UpsertEventSalesSummaryAsync(existingEventSummary, cancellationToken);
        }

        // ── Upsert TierSalesSummary via IReportingRepository ──────────────────
        var existingTierSummary = await reportingRepository
            .GetTierSalesSummaryAsync(payload.PricingTierId, cancellationToken);

        if (existingTierSummary == null)
        {
            var newTierSummary = new TierSalesSummary
            {
                PricingTierId     = tier.Id,
                EventId           = @event.Id,
                TierName          = tier.Name,
                UnitPrice         = tier.Price,
                TotalQuantity     = tier.TotalQuantity,
                QuantitySold      = payload.Quantity,
                QuantityAvailable = tier.AvailableQuantity,
                Revenue           = payload.TotalPrice,
                LastUpdatedAt     = DateTime.UtcNow
            };

            await reportingRepository.UpsertTierSalesSummaryAsync(newTierSummary, cancellationToken);
        }
        else
        {
            existingTierSummary.QuantitySold      += payload.Quantity;
            existingTierSummary.QuantityAvailable  = tier.AvailableQuantity;
            existingTierSummary.Revenue           += payload.TotalPrice;
            existingTierSummary.TotalQuantity      = tier.TotalQuantity;
            existingTierSummary.LastUpdatedAt      = DateTime.UtcNow;

            await reportingRepository.UpsertTierSalesSummaryAsync(existingTierSummary, cancellationToken);
        }

        _logger.LogInformation(
            "OutboxProcessor: applied TicketPurchased for event {EventId}, tier {TierId}, qty {Qty}.",
            payload.EventId, payload.PricingTierId, payload.Quantity);
    }

    /// <summary>
    /// Strongly-typed DTO matching the JSON payload written by TicketRepository.PurchaseAsync.
    /// </summary>
    private sealed record TicketPurchasedPayload(
        Guid TicketId,
        Guid EventId,
        Guid PricingTierId,
        string TierName,
        int Quantity,
        decimal UnitPrice,
        decimal TotalPrice,
        string PurchaserEmail,
        DateTime PurchasedAt);
}


