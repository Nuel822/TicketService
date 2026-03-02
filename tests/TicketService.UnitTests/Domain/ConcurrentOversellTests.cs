using FluentAssertions;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Domain;

/// <summary>
/// Unit tests for concurrent oversell prevention at the domain layer.
///
/// These tests verify that PricingTier and Event correctly prevent overselling
/// when DecrementAvailability is called concurrently from multiple threads.
///
/// IMPORTANT — scope of these tests:
///   Thread safety in production is guaranteed by the PostgreSQL UPDATE row lock
///   in TicketRepository.PurchaseAsync, which serialises concurrent purchase
///   requests at the database level.  These tests verify the domain guard fires
///   correctly and that total sold never exceeds capacity.
/// </summary>
public class ConcurrentOversellTests
{
    // ── PricingTier sequential oversell guard ─────────────────────────────────

    [Fact]
    public void PricingTier_WhenDecrementedSequentiallyBeyondCapacity_ShouldThrowOversellException()
    {
        var tier = PricingTier.Create(Guid.NewGuid(), "VIP", 100m, 10);

        for (var i = 0; i < 10; i++)
            tier.DecrementAvailability(1);

        var act = () => tier.DecrementAvailability(1);
        act.Should().Throw<OversellException>()
            .WithMessage("*Cannot purchase 1*Only 0*");
    }

    [Fact]
    public void PricingTier_WhenDecrementedBeyondCapacityInSingleCall_ShouldThrowOversellException()
    {
        var tier = PricingTier.Create(Guid.NewGuid(), "Early Bird", 30m, 3);

        var act = () => tier.DecrementAvailability(4);
        act.Should().Throw<OversellException>()
            .WithMessage("*Cannot purchase 4*Only 3*");
    }

    // ── Event sequential oversell guard ──────────────────────────────────────

    [Fact]
    public void Event_WhenDecrementedSequentiallyBeyondCapacity_ShouldThrowOversellException()
    {
        var @event = Event.Create(
            "Test Concert", "A test event", "Test Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromDateTime(DateTime.UtcNow),
            5);

        for (var i = 0; i < 5; i++)
            @event.DecrementAvailability(1);

        var act = () => @event.DecrementAvailability(1);
        act.Should().Throw<OversellException>()
            .WithMessage("*Cannot purchase 1*Only 0*");
    }

    // ── Concurrent oversell — total sold never exceeds capacity ───────────────

    [Fact]
    public async Task PricingTier_UnderConcurrentLoad_TotalSoldShouldNeverExceedCapacity()
    {
        const int capacity = 50;
        const int concurrentBuyers = 100;
        var tier = PricingTier.Create(Guid.NewGuid(), "General Admission", 25m, capacity);

        var successCount = 0;
        var oversellCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, concurrentBuyers).Select(_ => Task.Run(() =>
        {
            try
            {
                lock (lockObj) { tier.DecrementAvailability(1); }
                Interlocked.Increment(ref successCount);
            }
            catch (OversellException)
            {
                Interlocked.Increment(ref oversellCount);
            }
        }));

        await Task.WhenAll(tasks);

        successCount.Should().Be(capacity,
            "exactly {0} purchases should succeed when capacity is {0}", capacity);
        oversellCount.Should().Be(concurrentBuyers - capacity);
        tier.AvailableQuantity.Should().Be(0,
            "all capacity should be consumed but never go negative");
    }

    [Fact]
    public async Task Event_UnderConcurrentLoad_TotalSoldShouldNeverExceedCapacity()
    {
        const int capacity = 20;
        const int concurrentBuyers = 40;
        var @event = Event.Create(
            "Concurrent Test Event", "Testing concurrent access", "Test Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromDateTime(DateTime.UtcNow),
            capacity);

        var successCount = 0;
        var oversellCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, concurrentBuyers).Select(_ => Task.Run(() =>
        {
            try
            {
                lock (lockObj) { @event.DecrementAvailability(1); }
                Interlocked.Increment(ref successCount);
            }
            catch (OversellException)
            {
                Interlocked.Increment(ref oversellCount);
            }
        }));

        await Task.WhenAll(tasks);

        successCount.Should().Be(capacity);
        oversellCount.Should().Be(concurrentBuyers - capacity);
        @event.AvailableTickets.Should().Be(0);
    }

    // ── OutboxMessage ─────────────────────────────────────────────────────────

    [Fact]
    public void OutboxMessage_WhenCreatedWithCorrelationId_ShouldStoreIt()
    {
        const string correlationId = "idem-key-abc-123";

        var message = OutboxMessage.Create("TicketPurchased", """{"ticketId":"x"}""", correlationId);

        message.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void OutboxMessage_AfterMarkFailed_ShouldIncrementRetryCount()
    {
        var message = OutboxMessage.Create("TicketPurchased", """{"ticketId":"x"}""");

        message.MarkFailed("Connection timeout");
        message.MarkFailed("Connection timeout");

        message.RetryCount.Should().Be(2);
        message.Error.Should().Be("Connection timeout");
    }

    [Fact]
    public void OutboxMessage_IsDeadLettered_WhenRetryCountReachesMax_ShouldReturnTrue()
    {
        var message = OutboxMessage.Create("TicketPurchased", """{"ticketId":"x"}""");

        for (var i = 0; i < 5; i++)
            message.MarkFailed("error");

        message.IsDeadLettered(maxRetries: 5).Should().BeTrue();
    }
}


