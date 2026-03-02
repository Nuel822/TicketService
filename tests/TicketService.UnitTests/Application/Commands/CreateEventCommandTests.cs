using FluentAssertions;
using Moq;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Events.Commands;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Application.Commands;

public class CreateEventCommandTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly CreateEventCommand _command;

    public CreateEventCommandTests()
    {
        _command = new CreateEventCommand(_eventRepositoryMock.Object);
    }

    private static CreateEventRequest ValidRequest(int capacity = 100) =>
        new(
            Name: "Summer Festival",
            Description: "A great event",
            Venue: "Hyde Park",
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Time: TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: capacity,
            PricingTiers: new List<CreatePricingTierRequest>
            {
                new("General", 50m, capacity)
            });

    [Fact]
    public async Task ExecuteAsync_ShouldCallAddAsync_WithCorrectEvent()
    {
        _eventRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        var request = ValidRequest();
        await _command.ExecuteAsync(request);

        _eventRepositoryMock.Verify(
            r => r.AddAsync(It.Is<Event>(e => e.Name == "Summer Festival"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnResponse_WithCorrectFields()
    {
        _eventRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        var request = ValidRequest(capacity: 200);
        var response = await _command.ExecuteAsync(request);

        response.Name.Should().Be("Summer Festival");
        response.TotalCapacity.Should().Be(200);
        response.AvailableTickets.Should().Be(200);
        response.PricingTiers.Should().HaveCount(1);
        response.PricingTiers[0].Name.Should().Be("General");
        response.PricingTiers[0].Price.Should().Be(50m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreatePricingTiers_ForEachTierRequest()
    {
        _eventRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        var request = new CreateEventRequest(
            "Festival", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 100,
            PricingTiers: new List<CreatePricingTierRequest>
            {
                new("GA", 50m, 60),
                new("VIP", 150m, 40)
            });

        var response = await _command.ExecuteAsync(request);

        response.PricingTiers.Should().HaveCount(2);
        response.PricingTiers.Should().Contain(t => t.Name == "GA" && t.Price == 50m && t.TotalQuantity == 60);
        response.PricingTiers.Should().Contain(t => t.Name == "VIP" && t.Price == 150m && t.TotalQuantity == 40);
    }

    // ── Duplicate venue/date/time guard ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenVenueDateTimeConflict_ShouldThrowDuplicateEventException()
    {
        _eventRepositoryMock
            .Setup(r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _command.ExecuteAsync(ValidRequest());

        await act.Should().ThrowAsync<DuplicateEventException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoVenueDateTimeConflict_ShouldPersistEvent()
    {
        _eventRepositoryMock
            .Setup(r => r.ExistsAtVenueAndDateTimeAsync(
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _eventRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        await _command.ExecuteAsync(ValidRequest());

        _eventRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}


