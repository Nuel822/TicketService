using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Events.Queries;
using TicketService.Domain.Entities;

namespace TicketService.UnitTests.Application.Queries;

public class GetEventByIdQueryTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly GetEventByIdQuery _query;

    public GetEventByIdQueryTests()
    {
        _query = new GetEventByIdQuery(_eventRepositoryMock.Object);
    }

    private static Event CreateEvent(string name = "Rock Night", int capacity = 100)
    {
        var @event = Event.Create(
            name, "Description", "O2 Arena",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            new TimeOnly(20, 0),
            capacity);

        @event.PricingTiers.Add(PricingTier.Create(@event.Id, "General", 50m, capacity));
        return @event;
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventExists_ShouldReturnMappedResponse()
    {
        var @event = CreateEvent("Jazz Night", 200);

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var result = await _query.ExecuteAsync(@event.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(@event.Id);
        result.Name.Should().Be("Jazz Night");
        result.TotalCapacity.Should().Be(200);
        result.AvailableTickets.Should().Be(200);
        result.PricingTiers.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventDoesNotExist_ShouldThrowNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var act = async () => await _query.ExecuteAsync(nonExistentId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event*not found*");
    }

}


