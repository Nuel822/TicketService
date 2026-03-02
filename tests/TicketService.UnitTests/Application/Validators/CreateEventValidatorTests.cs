using FluentAssertions;
using FluentValidation.TestHelper;
using TicketService.Application.Events.Commands;
using TicketService.Application.Events.Validators;

namespace TicketService.UnitTests.Application.Validators;

public class CreateEventValidatorTests
{
    private readonly CreateEventValidator _validator = new();

    private static CreateEventRequest ValidRequest(
        int totalCapacity = 100,
        IReadOnlyList<CreatePricingTierRequest>? tiers = null) =>
        new(
            Name: "Summer Festival",
            Description: "A great event",
            Venue: "Hyde Park",
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Time: TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: totalCapacity,
            PricingTiers: tiers ?? new List<CreatePricingTierRequest>
            {
                new("General", 50m, totalCapacity)
            });

    // ── Name ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Name = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WhenExceeds200Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Name = new string('A', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ── Description ───────────────────────────────────────────────────────────

    [Fact]
    public void Description_WhenNull_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Description = null! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_WhenExceeds2000Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Description = new string('D', 2001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    // ── Venue ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Venue_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Venue = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Venue);
    }

    // ── Date ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_WhenInThePast_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Fact]
    public void Date_WhenToday_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    // ── TotalCapacity ─────────────────────────────────────────────────────────

    [Fact]
    public void TotalCapacity_WhenZero_ShouldHaveValidationError()
    {
        var request = new CreateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 0,
            PricingTiers: new List<CreatePricingTierRequest> { new("GA", 10m, 0) });
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TotalCapacity);
    }

    // ── PricingTiers ──────────────────────────────────────────────────────────

    [Fact]
    public void PricingTiers_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PricingTiers = new List<CreatePricingTierRequest>() };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PricingTiers);
    }

    [Fact]
    public void PricingTiers_WhenTierQuantitySumDoesNotMatchCapacity_ShouldHaveValidationError()
    {
        var request = new CreateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 100,
            PricingTiers: new List<CreatePricingTierRequest>
            {
                new("GA", 50m, 60),   // 60 + 60 = 120 ≠ 100
                new("VIP", 100m, 60)
            });

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void PricingTiers_WhenTierQuantitySumMatchesCapacity_ShouldNotHaveValidationError()
    {
        var request = new CreateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 100,
            PricingTiers: new List<CreatePricingTierRequest>
            {
                new("GA", 50m, 80),
                new("VIP", 100m, 20)
            });

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PricingTiers_WhenTierPriceIsNegative_ShouldHaveValidationError()
    {
        var request = new CreateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 100,
            PricingTiers: new List<CreatePricingTierRequest>
            {
                new("GA", -1m, 100)
            });

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("PricingTiers[0].Price");
    }

    [Fact]
    public void PricingTiers_WhenTierPriceIsZero_ShouldNotHaveValidationError()
    {
        // Free events are valid (price >= 0)
        var request = ValidRequest(tiers: new List<CreatePricingTierRequest>
        {
            new("Free Tier", 0m, 100)
        });

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor("PricingTiers[0].Price");
    }
}


