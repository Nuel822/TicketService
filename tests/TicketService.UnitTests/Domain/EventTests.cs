using FluentAssertions;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Domain;

public class EventTests
{
    private static Event CreateEvent(int totalCapacity = 100)
        => Event.Create("Test Event", "A description", "Test Venue", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)), totalCapacity);

    // ── Factory method ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetAvailableTicketsEqualToTotalCapacity()
    {
        var @event = CreateEvent(totalCapacity: 200);

        @event.AvailableTickets.Should().Be(200);
        @event.TotalCapacity.Should().Be(200);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WhenNoTicketsSold_ShouldSetAvailableTicketsToNewCapacity()
    {
        var @event = CreateEvent(totalCapacity: 100);

        @event.Update("New Name", "New Desc", "New Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)),
            totalCapacity: 150);

        @event.TotalCapacity.Should().Be(150);
        @event.AvailableTickets.Should().Be(150);
    }

    [Fact]
    public void Update_WhenSomeTicketsSold_ShouldPreserveSoldCount()
    {
        var @event = CreateEvent(totalCapacity: 100);
        @event.DecrementAvailability(30); // 30 sold, 70 available

        @event.Update("New Name", "New Desc", "New Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)),
            totalCapacity: 120);

        @event.TotalCapacity.Should().Be(120);
        @event.AvailableTickets.Should().Be(90); // 120 - 30 sold
    }

    [Fact]
    public void Update_WhenNewCapacityLessThanSoldTickets_ShouldThrowOversellException()
    {
        var @event = CreateEvent(totalCapacity: 100);
        @event.DecrementAvailability(80); // 80 sold

        var act = () => @event.Update("Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)),
            totalCapacity: 50); // less than 80 sold

        act.Should().Throw<OversellException>();
    }

    // ── DecrementAvailability ─────────────────────────────────────────────────

    [Fact]
    public void DecrementAvailability_WhenSufficientStock_ShouldReduceAvailableTickets()
    {
        var @event = CreateEvent(totalCapacity: 100);

        @event.DecrementAvailability(10);

        @event.AvailableTickets.Should().Be(90);
    }

    [Fact]
    public void DecrementAvailability_WhenExactlyAvailable_ShouldReduceToZero()
    {
        var @event = CreateEvent(totalCapacity: 50);

        @event.DecrementAvailability(50);

        @event.AvailableTickets.Should().Be(0);
    }

    [Fact]
    public void DecrementAvailability_WhenInsufficientStock_ShouldThrowOversellException()
    {
        var @event = CreateEvent(totalCapacity: 10);

        var act = () => @event.DecrementAvailability(11);

        act.Should().Throw<OversellException>();
    }

    // ── IncrementAvailability ─────────────────────────────────────────────────

    [Fact]
    public void IncrementAvailability_ShouldIncreaseAvailableTickets()
    {
        var @event = CreateEvent(totalCapacity: 100);
        @event.DecrementAvailability(20);

        @event.IncrementAvailability(5);

        @event.AvailableTickets.Should().Be(85);
    }

    [Fact]
    public void IncrementAvailability_WhenExceedsTotalCapacity_ShouldThrowOversellException()
    {
        var @event = CreateEvent(totalCapacity: 100);
        // AvailableTickets == 100 (nothing sold); incrementing by 1 would exceed capacity

        var act = () => @event.IncrementAvailability(1);

        act.Should().Throw<OversellException>();
    }
}


