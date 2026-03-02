using FluentAssertions;
using TicketService.Domain.Entities;
using TicketService.Domain.Exceptions;

namespace TicketService.UnitTests.Domain;

public class PricingTierTests
{
    private static PricingTier CreateTier(int quantity = 50, decimal price = 25m)
        => PricingTier.Create(Guid.NewGuid(), "General Admission", price, quantity);

    // ── Factory method ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetAvailableQuantityEqualToTotalQuantity()
    {
        var tier = CreateTier(quantity: 100);

        tier.AvailableQuantity.Should().Be(100);
        tier.TotalQuantity.Should().Be(100);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WhenNoTicketsSold_ShouldSetAvailableQuantityToNewTotal()
    {
        var tier = CreateTier(quantity: 50);

        tier.Update("VIP", 100m, 80);

        tier.TotalQuantity.Should().Be(80);
        tier.AvailableQuantity.Should().Be(80);
        tier.Name.Should().Be("VIP");
        tier.Price.Should().Be(100m);
    }

    [Fact]
    public void Update_WhenSomeTicketsSold_ShouldPreserveSoldCount()
    {
        var tier = CreateTier(quantity: 50);
        tier.DecrementAvailability(20); // 20 sold, 30 available

        tier.Update("VIP", 100m, 60);

        tier.TotalQuantity.Should().Be(60);
        tier.AvailableQuantity.Should().Be(40); // 60 - 20 sold
    }

    [Fact]
    public void Update_WhenNewTotalLessThanSold_ShouldThrowOversellException()
    {
        var tier = CreateTier(quantity: 50);
        tier.DecrementAvailability(40); // 40 sold, 10 available

        var act = () => tier.Update("VIP", 100m, 30); // 30 < 40 sold → oversell

        act.Should().Throw<OversellException>();
    }

    // ── DecrementAvailability ─────────────────────────────────────────────────

    [Fact]
    public void DecrementAvailability_WhenSufficientStock_ShouldReduceAvailableQuantity()
    {
        var tier = CreateTier(quantity: 50);

        tier.DecrementAvailability(10);

        tier.AvailableQuantity.Should().Be(40);
    }

    [Fact]
    public void DecrementAvailability_WhenExactlyAvailable_ShouldReduceToZero()
    {
        var tier = CreateTier(quantity: 50);

        tier.DecrementAvailability(50);

        tier.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public void DecrementAvailability_WhenInsufficientStock_ShouldThrowOversellException()
    {
        var tier = CreateTier(quantity: 5);

        var act = () => tier.DecrementAvailability(6);

        act.Should().Throw<OversellException>()
            .WithMessage("*Cannot purchase 6 ticket(s)*");
    }

    // ── IncrementAvailability ─────────────────────────────────────────────────

    [Fact]
    public void IncrementAvailability_ShouldIncreaseAvailableQuantity()
    {
        var tier = CreateTier(quantity: 50);
        tier.DecrementAvailability(10);

        tier.IncrementAvailability(5);

        tier.AvailableQuantity.Should().Be(45);
    }
}


