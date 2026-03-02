using FluentAssertions;
using TicketService.Domain.Entities;
using TicketService.Domain.Enums;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Domain;

public class TicketTests
{
    private static Ticket CreateActiveTicket(int quantity = 2, decimal unitPrice = 50m)
        => Ticket.Create(Guid.NewGuid(), Guid.NewGuid(), "Alice Smith", "alice@example.com", quantity, unitPrice);

    // ── Factory method ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetStatusToActive()
    {
        var ticket = CreateActiveTicket();

        ticket.Status.Should().Be(TicketStatus.Active);
    }

    [Fact]
    public void Create_ShouldCalculateTotalPriceCorrectly()
    {
        var ticket = CreateActiveTicket(quantity: 3, unitPrice: 25m);

        ticket.TotalPrice.Should().Be(75m);
        ticket.UnitPrice.Should().Be(25m);
        ticket.Quantity.Should().Be(3);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenActive_ShouldSetStatusToCancelled()
    {
        var ticket = CreateActiveTicket();

        ticket.Cancel();

        ticket.Status.Should().Be(TicketStatus.Cancelled);
        ticket.CancelledAt.Should().NotBeNull();
        ticket.CancelledAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrowInvalidTicketStateException()
    {
        var ticket = CreateActiveTicket();
        ticket.Cancel();

        var act = () => ticket.Cancel();

        act.Should().Throw<InvalidTicketStateException>()
            .WithMessage("*already cancelled*");
    }

    [Fact]
    public void Cancel_WhenAlreadyRefunded_ShouldThrowInvalidTicketStateException()
    {
        var ticket = CreateActiveTicket();
        ticket.Refund();

        var act = () => ticket.Cancel();

        act.Should().Throw<InvalidTicketStateException>()
            .WithMessage("*refunded*");
    }

    // ── Refund ────────────────────────────────────────────────────────────────

    [Fact]
    public void Refund_WhenActive_ShouldSetStatusToRefunded()
    {
        var ticket = CreateActiveTicket();

        ticket.Refund();

        ticket.Status.Should().Be(TicketStatus.Refunded);
        ticket.RefundedAt.Should().NotBeNull();
        ticket.RefundedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        ticket.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void Refund_WhenCancelled_ShouldSetStatusToRefunded()
    {
        // Refund is allowed on a cancelled ticket (e.g. refund after cancellation)
        var ticket = CreateActiveTicket();
        ticket.Cancel();

        ticket.Refund();

        ticket.Status.Should().Be(TicketStatus.Refunded);
    }

    [Fact]
    public void Refund_WhenAlreadyRefunded_ShouldThrowInvalidTicketStateException()
    {
        var ticket = CreateActiveTicket();
        ticket.Refund();

        var act = () => ticket.Refund();

        act.Should().Throw<InvalidTicketStateException>()
            .WithMessage("*already been refunded*");
    }
}


