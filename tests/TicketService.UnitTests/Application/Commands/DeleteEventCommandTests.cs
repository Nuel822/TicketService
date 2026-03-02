using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Events.Commands;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Application.Commands;

public class DeleteEventCommandTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<ITicketRepository> _ticketRepositoryMock = new();
    private readonly DeleteEventCommand _command;

    public DeleteEventCommandTests()
    {
        _command = new DeleteEventCommand(_eventRepositoryMock.Object, _ticketRepositoryMock.Object);
    }

    private static Event MakeEvent() =>
        Event.Create("Test Event", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)), 100);

    [Fact]
    public async Task ExecuteAsync_WhenEventNotFound_ShouldThrowNotFoundException()
    {
        var eventId = Guid.NewGuid();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var act = async () => await _command.ExecuteAsync(eventId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventHasSoldTickets_ShouldThrowEventHasActiveTicketsException()
    {
        var @event = MakeEvent();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);
        _ticketRepositoryMock
            .Setup(r => r.HasSoldTicketsAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _command.ExecuteAsync(@event.Id);

        await act.Should().ThrowAsync<EventHasActiveTicketsException>()
            .WithMessage("*active ticket holders*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventHasNoSoldTickets_ShouldCallDeleteAsync()
    {
        var @event = MakeEvent();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);
        _ticketRepositoryMock
            .Setup(r => r.HasSoldTicketsAsync(@event.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _eventRepositoryMock
            .Setup(r => r.DeleteAsync(@event, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _command.ExecuteAsync(@event.Id);

        _eventRepositoryMock.Verify(
            r => r.DeleteAsync(@event, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}


