using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Events.Commands;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Application.Commands;

public class UpdateEventCommandTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly UpdateEventCommand _command;

    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    private static readonly TimeOnly EventTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(18));

    public UpdateEventCommandTests()
    {
        _command = new UpdateEventCommand(_eventRepositoryMock.Object);
    }

    private static Event MakeEvent(string venue = "Hyde Park") =>
        Event.Create("Summer Festival", "A great event", venue, FutureDate, EventTime, 100);

    private static UpdateEventRequest ValidRequest(
        string venue = "Hyde Park",
        DateOnly? date = null,
        TimeOnly? time = null) =>
        new(
            Name: "Summer Festival (Updated)",
            Description: "Updated description",
            Venue: venue,
            Date: date ?? FutureDate,
            Time: time ?? EventTime,
            TotalCapacity: 100,
            PricingTiers: new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: null, Name: "General", Price: 50m, Quantity: 100)
            });

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenEventNotFound_ShouldThrowNotFoundException()
    {
        var eventId = Guid.NewGuid();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var act = () => _command.ExecuteAsync(eventId, ValidRequest());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event*");
    }

    // ── Duplicate venue/date/time guard ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenAnotherEventOccupiesVenueDateAndTime_ShouldThrowDuplicateEventException()
    {
        var @event = MakeEvent();

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _eventRepositoryMock
            .Setup(r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                @event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _command.ExecuteAsync(@event.Id, ValidRequest());

        await act.Should().ThrowAsync<DuplicateEventException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoOtherEventOccupiesVenueDateAndTime_ShouldNotThrow()
    {
        var @event = MakeEvent();

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _eventRepositoryMock
            .Setup(r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                @event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _eventRepositoryMock
            .Setup(r => r.UpdateAsync(@event, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var act = () => _command.ExecuteAsync(@event.Id, ValidRequest());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateCheck_ShouldExcludeCurrentEventId()
    {
        // Verifies that the self-event is excluded from the conflict check so an event
        // can be updated without conflicting with itself.
        var @event = MakeEvent();

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _eventRepositoryMock
            .Setup(r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                @event.Id,                          // must pass the current event's ID as excludeId
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _eventRepositoryMock
            .Setup(r => r.UpdateAsync(@event, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.ExecuteAsync(@event.Id, ValidRequest());

        _eventRepositoryMock.Verify(
            r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                @event.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

