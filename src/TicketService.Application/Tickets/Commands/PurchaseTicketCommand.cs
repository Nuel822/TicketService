using System.Text.Json;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Common.Exceptions;
using TicketService.Domain.Entities;
using TicketService.Domain.Enums;
using TicketService.Domain.Exceptions;

namespace TicketService.Application.Tickets.Commands;

public record PurchaseTicketRequest(
    Guid PricingTierId,
    string PurchaserName,
    string PurchaserEmail,
    int Quantity);

public record PurchaseTicketResponse(
    Guid TicketId,
    Guid EventId,
    Guid PricingTierId,
    string TierName,
    string PurchaserName,
    string PurchaserEmail,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    TicketStatus Status,
    DateTime PurchasedAt);

/// <summary>
/// Wraps the purchase response with a flag indicating whether this was a replayed
/// idempotent response (true) or a freshly executed purchase (false).
/// The API layer uses this to return 200 OK for replays and 201 Created for new purchases.
/// </summary>
public record PurchaseTicketResult(PurchaseTicketResponse Response, bool IsReplay);

public class PurchaseTicketCommand
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IIdempotencyStore _idempotencyStore;

    public PurchaseTicketCommand(
        ITicketRepository ticketRepository,
        IEventRepository eventRepository,
        IIdempotencyStore idempotencyStore)
    {
        _ticketRepository = ticketRepository;
        _eventRepository = eventRepository;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<PurchaseTicketResult> ExecuteAsync(
        Guid eventId,
        PurchaseTicketRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var hasKey = !string.IsNullOrWhiteSpace(idempotencyKey);

        // ── Idempotency check ─────────────────────────────────────────────────
        if (hasKey)
        {
            var replay = await HandleIdempotencyAsync(idempotencyKey!, cancellationToken);
            if (replay is not null)
                return replay;
        }

        // ── Validate event and tier ───────────────────────────────────────────
        var eventExists = await _eventRepository.ExistsAsync(eventId, cancellationToken);
        if (!eventExists)
            throw new NotFoundException(nameof(Domain.Entities.Event), eventId);

        var tier = await _ticketRepository.GetPricingTierByIdAsync(request.PricingTierId, cancellationToken);
        if (tier == null || tier.EventId != eventId)
            throw new NotFoundException(nameof(Domain.Entities.PricingTier), request.PricingTierId);

        // ── Optimistic pre-check (fast-fail before acquiring the DB lock) ─────
        // HasAvailability reads the in-memory snapshot loaded above.
        // It is NOT the authoritative oversell guard — the pessimistic SQL lock
        // inside PurchaseAsync is. However, it avoids opening a transaction for
        // requests that are obviously impossible (e.g. 1000 tickets when 5 remain),
        // saving a round-trip under normal (non-racing) conditions.
        // Under concurrent load the snapshot may be stale, so the SQL lock is
        // still required to prevent actual overselling.
        if (!tier.HasAvailability(request.Quantity))
            throw new OversellException(tier.Name, request.Quantity, tier.AvailableQuantity);

        // ── Execute purchase (pessimistic lock inside repository) ─────────────
        var ticket = await _ticketRepository.PurchaseAsync(
            eventId,
            request.PricingTierId,
            request.PurchaserName,
            request.PurchaserEmail,
            request.Quantity,
            idempotencyKey,
            cancellationToken);

        var response = new PurchaseTicketResponse(
            ticket.Id,
            ticket.EventId,
            ticket.PricingTierId,
            tier.Name,
            ticket.PurchaserName,
            ticket.PurchaserEmail,
            ticket.Quantity,
            ticket.UnitPrice,
            ticket.TotalPrice,
            ticket.Status,
            ticket.PurchasedAt);

        // ── Persist idempotency record ────────────────────────────────────────
        // Stored after a successful purchase so retries with the same key replay
        // the original response instead of executing a second purchase.
        // SaveAsync silently swallows a unique-constraint race (two concurrent
        // requests with the same key) — the winner's record is returned to both.
        if (hasKey)
            await SaveIdempotencyRecordAsync(idempotencyKey!, eventId, response, cancellationToken);

        return new PurchaseTicketResult(response, IsReplay: false);
    }

    /// <summary>
    /// Checks the idempotency store for a previously cached response.
    /// Returns a <see cref="PurchaseTicketResult"/> with <c>IsReplay = true</c> if a
    /// non-expired record exists; returns <c>null</c> if this is a fresh request.
    /// </summary>
    private async Task<PurchaseTicketResult?> HandleIdempotencyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var cached = await _idempotencyStore.GetAsync(idempotencyKey, cancellationToken);
        if (cached is null)
            return null;

        var cachedResponse = JsonSerializer.Deserialize<PurchaseTicketResponse>(cached.ResponseBody)
            ?? throw new InvalidOperationException(
                $"Idempotency store contains an unparseable response body for key '{idempotencyKey}'.");

        return new PurchaseTicketResult(cachedResponse, IsReplay: true);
    }

    /// <summary>
    /// Persists the purchase response to the idempotency store so future retries
    /// with the same key replay the original response without re-executing the purchase.
    /// </summary>
    private async Task SaveIdempotencyRecordAsync(
        string idempotencyKey,
        Guid eventId,
        PurchaseTicketResponse response,
        CancellationToken cancellationToken)
    {
        var record = IdempotencyKey.Create(
            key: idempotencyKey,
            requestPath: $"/api/events/{eventId}/tickets",
            responseStatusCode: 201,
            responseBody: JsonSerializer.Serialize(response));

        await _idempotencyStore.SaveAsync(record, cancellationToken);
    }
}


