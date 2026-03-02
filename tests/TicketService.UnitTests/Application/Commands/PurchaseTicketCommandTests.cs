using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Tickets.Commands;
using TicketService.Domain.Entities;
using TicketService.Domain.Enums;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Application.Commands;

public class PurchaseTicketCommandTests
{
    private readonly Mock<ITicketRepository> _ticketRepositoryMock = new();
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();
    private readonly PurchaseTicketCommand _command;

    public PurchaseTicketCommandTests()
    {
        // Default: no cached record (cache miss) so all tests exercise the purchase path
        _idempotencyStoreMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);
        _idempotencyStoreMock
            .Setup(s => s.SaveAsync(It.IsAny<IdempotencyKey>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _command = new PurchaseTicketCommand(
            _ticketRepositoryMock.Object,
            _eventRepositoryMock.Object,
            _idempotencyStoreMock.Object);
    }

    private static PricingTier MakeTier(Guid eventId, int quantity = 100, decimal price = 50m)
        => PricingTier.Create(eventId, "General Admission", price, quantity);

    private static Ticket MakeTicket(Guid eventId, Guid tierId, int quantity = 2, decimal unitPrice = 50m)
        => Ticket.Create(eventId, tierId, "Alice Smith", "alice@example.com", quantity, unitPrice);

    [Fact]
    public async Task ExecuteAsync_WhenEventNotFound_ShouldThrowNotFoundException()
    {
        var eventId = Guid.NewGuid();
        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new PurchaseTicketRequest(Guid.NewGuid(), "Alice", "alice@example.com", 2);

        var act = async () => await _command.ExecuteAsync(eventId, request, null);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPricingTierNotFound_ShouldThrowNotFoundException()
    {
        var eventId = Guid.NewGuid();
        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ticketRepositoryMock
            .Setup(r => r.GetPricingTierByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingTier?)null);

        var request = new PurchaseTicketRequest(Guid.NewGuid(), "Alice", "alice@example.com", 2);

        var act = async () => await _command.ExecuteAsync(eventId, request, null);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PricingTier*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTierBelongsToDifferentEvent_ShouldThrowNotFoundException()
    {
        var eventId = Guid.NewGuid();
        var differentEventId = Guid.NewGuid();
        var tier = MakeTier(differentEventId); // tier belongs to a different event

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ticketRepositoryMock
            .Setup(r => r.GetPricingTierByIdAsync(tier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);

        var request = new PurchaseTicketRequest(tier.Id, "Alice", "alice@example.com", 2);

        var act = async () => await _command.ExecuteAsync(eventId, request, null);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PricingTier*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuantityExceedsAvailability_ShouldThrowOversellExceptionBeforeTransaction()
    {
        var eventId = Guid.NewGuid();
        // Tier has only 1 seat available; request asks for 5
        var tier = PricingTier.Create(eventId, "General Admission", 50m, 1);

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ticketRepositoryMock
            .Setup(r => r.GetPricingTierByIdAsync(tier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);

        var request = new PurchaseTicketRequest(tier.Id, "Alice", "alice@example.com", 5);

        var act = async () => await _command.ExecuteAsync(eventId, request, null);

        await act.Should().ThrowAsync<OversellException>();

        // PurchaseAsync must NOT be called — the pre-check short-circuits before the transaction
        _ticketRepositoryMock.Verify(
            r => r.PurchaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldReturnPurchaseTicketResponse()
    {
        var eventId = Guid.NewGuid();
        var tier = MakeTier(eventId, quantity: 100, price: 75m);
        var ticket = MakeTicket(eventId, tier.Id, quantity: 2, unitPrice: 75m);

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ticketRepositoryMock
            .Setup(r => r.GetPricingTierByIdAsync(tier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);
        _ticketRepositoryMock
            .Setup(r => r.PurchaseAsync(eventId, tier.Id, "Alice Smith", "alice@example.com", 2, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var request = new PurchaseTicketRequest(tier.Id, "Alice Smith", "alice@example.com", 2);
        var result = await _command.ExecuteAsync(eventId, request, null);

        result.IsReplay.Should().BeFalse();
        result.Response.EventId.Should().Be(eventId);
        result.Response.PricingTierId.Should().Be(tier.Id);
        result.Response.Quantity.Should().Be(2);
        result.Response.UnitPrice.Should().Be(75m);
        result.Response.TotalPrice.Should().Be(150m);
        result.Response.Status.Should().Be(TicketStatus.Active);
        result.Response.TierName.Should().Be("General Admission");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldCallPurchaseAsync_Once()
    {
        var eventId = Guid.NewGuid();
        var tier = MakeTier(eventId);
        var ticket = MakeTicket(eventId, tier.Id);

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ticketRepositoryMock
            .Setup(r => r.GetPricingTierByIdAsync(tier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tier);
        _ticketRepositoryMock
            .Setup(r => r.PurchaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var request = new PurchaseTicketRequest(tier.Id, "Alice Smith", "alice@example.com", 2);
        await _command.ExecuteAsync(eventId, request, "idempotency-key-123");

        _ticketRepositoryMock.Verify(
            r => r.PurchaseAsync(eventId, tier.Id, "Alice Smith", "alice@example.com", 2,
                "idempotency-key-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}


