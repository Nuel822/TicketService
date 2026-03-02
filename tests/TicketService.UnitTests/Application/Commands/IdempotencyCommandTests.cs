using System.Text.Json;
using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Tickets.Commands;
using TicketService.Domain.Entities;
using TicketService.Domain.Enums;

namespace TicketService.UnitTests.Application.Commands;

/// <summary>
/// Verifies the full idempotency contract of PurchaseTicketCommand:
///   1. Cache miss  → executes purchase, saves record, returns IsReplay=false
///   2. Cache hit   → returns cached response, skips purchase, returns IsReplay=true
///   3. Null key    → skips idempotency store entirely, executes purchase
///   4. Event/tier not found → NotFoundException thrown (store not consulted for missing event)
/// </summary>
public class IdempotencyCommandTests
{
    private readonly Mock<ITicketRepository> _ticketRepoMock = new();
    private readonly Mock<IEventRepository> _eventRepoMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();

    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _tierId = Guid.NewGuid();

    private PurchaseTicketCommand CreateCommand() =>
        new(_ticketRepoMock.Object, _eventRepoMock.Object, _idempotencyStoreMock.Object);

    /// <summary>Sets up a full happy-path: event exists, tier exists, purchase succeeds.</summary>
    private void SetupHappyPath(string? idempotencyKey = null)
    {
        var tier = PricingTier.Create(_eventId, "General Admission", 25.00m, 100);
        var ticket = Ticket.Create(_eventId, _tierId, "Alice Smith", "alice@example.com", 2, 25.00m);

        _eventRepoMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ticketRepoMock
            .Setup(r => r.GetPricingTierByIdAsync(_tierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);

        _ticketRepoMock
            .Setup(r => r.PurchaseAsync(
                _eventId, _tierId,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        // No cached record — this is a fresh request
        _idempotencyStoreMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        _idempotencyStoreMock
            .Setup(s => s.SaveAsync(It.IsAny<IdempotencyKey>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Cache miss: fresh purchase ────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CacheMiss_ShouldExecutePurchaseAndReturnIsReplayFalse()
    {
        const string key = "idem-key-abc-123";
        SetupHappyPath(key);

        var request = new PurchaseTicketRequest(_tierId, "Alice Smith", "alice@example.com", 2);
        var result = await CreateCommand().ExecuteAsync(_eventId, request, key);

        result.IsReplay.Should().BeFalse();
        _ticketRepoMock.Verify(r => r.PurchaseAsync(
            _eventId, _tierId, "Alice Smith", "alice@example.com", 2,
            key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CacheMiss_ShouldSaveIdempotencyRecord()
    {
        const string key = "idem-key-save-test";
        SetupHappyPath(key);

        var request = new PurchaseTicketRequest(_tierId, "Alice Smith", "alice@example.com", 2);
        await CreateCommand().ExecuteAsync(_eventId, request, key);

        _idempotencyStoreMock.Verify(
            s => s.SaveAsync(It.Is<IdempotencyKey>(k => k.Key == key), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Cache hit: replay ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CacheHit_ShouldReturnCachedResponseWithIsReplayTrue()
    {
        const string key = "idem-key-replay";

        // Build a cached response that the store will return
        var cachedResponse = new PurchaseTicketResponse(
            Guid.NewGuid(), _eventId, _tierId, "VIP", "Alice", "alice@example.com",
            1, 100m, 100m, TicketStatus.Active, DateTime.UtcNow.AddMinutes(-5));

        var cachedRecord = IdempotencyKey.Create(
            key,
            $"/api/events/{_eventId}/tickets",
            201,
            JsonSerializer.Serialize(cachedResponse));

        _idempotencyStoreMock
            .Setup(s => s.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedRecord);

        var request = new PurchaseTicketRequest(_tierId, "Alice", "alice@example.com", 1);
        var result = await CreateCommand().ExecuteAsync(_eventId, request, key);

        result.IsReplay.Should().BeTrue();
        result.Response.TicketId.Should().Be(cachedResponse.TicketId);

        // Purchase must NOT be executed on a replay
        _ticketRepoMock.Verify(
            r => r.PurchaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Null key: idempotency store bypassed ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenKeyIsNull_ShouldSkipIdempotencyStoreAndExecutePurchase()
    {
        SetupHappyPath(null);

        var request = new PurchaseTicketRequest(_tierId, "Bob Jones", "bob@example.com", 1);
        var result = await CreateCommand().ExecuteAsync(_eventId, request, null);

        result.IsReplay.Should().BeFalse();

        // Store must never be consulted when no key is supplied
        _idempotencyStoreMock.Verify(
            s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _idempotencyStoreMock.Verify(
            s => s.SaveAsync(It.IsAny<IdempotencyKey>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Validation errors ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenEventNotFound_ShouldThrowNotFoundException()
    {
        _idempotencyStoreMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        _eventRepoMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateCommand()
            .Invoking(c => c.ExecuteAsync(_eventId,
                new PurchaseTicketRequest(_tierId, "Alice", "alice@example.com", 2),
                "some-key"))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTierNotFound_ShouldThrowNotFoundException()
    {
        _idempotencyStoreMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        _eventRepoMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ticketRepoMock
            .Setup(r => r.GetPricingTierByIdAsync(_tierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingTier?)null);

        await CreateCommand()
            .Invoking(c => c.ExecuteAsync(_eventId,
                new PurchaseTicketRequest(_tierId, "Alice", "alice@example.com", 2),
                "some-key"))
            .Should().ThrowAsync<NotFoundException>();
    }
}


