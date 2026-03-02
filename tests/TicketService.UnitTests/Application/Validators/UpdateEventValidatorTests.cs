using FluentValidation.TestHelper;
using TicketService.Application.Events.Commands;
using TicketService.Application.Events.Validators;

namespace TicketService.UnitTests.Application.Validators;

public class UpdateEventValidatorTests
{
    private readonly UpdateEventValidator _validator = new();

    private static UpdateEventRequest ValidRequest(
        int totalCapacity = 100,
        IReadOnlyList<UpdatePricingTierRequest>? tiers = null) =>
        new(
            Name: "Summer Festival",
            Description: "A great event",
            Venue: "Hyde Park",
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Time: TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: totalCapacity,
            PricingTiers: tiers ?? new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: Guid.NewGuid(), Name: "General", Price: 50m, Quantity: totalCapacity)
            });

    // ── Name ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Name = "" };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_WhenExceeds200Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Name = new string('A', 201) };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ── Description ───────────────────────────────────────────────────────────

    [Fact]
    public void Description_WhenNull_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Description = null! };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_WhenExceeds2000Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Description = new string('D', 2001) };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Description);
    }

    // ── Venue ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Venue_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Venue = "" };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Venue);
    }

    [Fact]
    public void Venue_WhenExceeds300Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Venue = new string('V', 301) };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Venue);
    }

    // ── Date ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_WhenInThePast_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Date);
    }

    // ── TotalCapacity ─────────────────────────────────────────────────────────

    [Fact]
    public void TotalCapacity_WhenZero_ShouldHaveValidationError()
    {
        var request = new UpdateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 0,
            PricingTiers: new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: null, Name: "GA", Price: 10m, Quantity: 0)
            });
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.TotalCapacity);
    }

    // ── PricingTiers — collection-level ───────────────────────────────────────

    [Fact]
    public void PricingTiers_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PricingTiers = new List<UpdatePricingTierRequest>() };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PricingTiers);
    }

    // ── PricingTiers — tier-level field rules ─────────────────────────────────

    [Fact]
    public void TierName_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest(tiers: new List<UpdatePricingTierRequest>
        {
            new(ExistingTierId: Guid.NewGuid(), Name: "", Price: 50m, Quantity: 100)
        });
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("PricingTiers[0].Name");
    }

    [Fact]
    public void TierPrice_WhenNegative_ShouldHaveValidationError()
    {
        var request = ValidRequest(tiers: new List<UpdatePricingTierRequest>
        {
            new(ExistingTierId: Guid.NewGuid(), Name: "GA", Price: -0.01m, Quantity: 100)
        });
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("PricingTiers[0].Price");
    }

    [Fact]
    public void TierQuantity_WhenZero_ShouldHaveValidationError()
    {
        var request = ValidRequest(tiers: new List<UpdatePricingTierRequest>
        {
            new(ExistingTierId: Guid.NewGuid(), Name: "GA", Price: 50m, Quantity: 0)
        });
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("PricingTiers[0].Quantity");
    }

    // ── Cross-field: tier quantity sum vs totalCapacity ────────────────────────

    [Fact]
    public void TierQuantitySum_WhenExceedsTotalCapacity_ShouldHaveValidationError()
    {
        var request = new UpdateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 1000,
            PricingTiers: new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: Guid.NewGuid(), Name: "GA",  Price: 49.99m, Quantity: 600),
                new(ExistingTierId: null,            Name: "VIP", Price: 99.99m, Quantity: 600)
            });

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void TierQuantitySum_WhenEqualsTotalCapacity_ShouldNotHaveValidationError()
    {
        var request = new UpdateEventRequest(
            "Name", "Desc", "Venue",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 1000,
            PricingTiers: new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: Guid.NewGuid(), Name: "GA",  Price: 49.99m, Quantity: 400),
                new(ExistingTierId: null,            Name: "VIP", Price: 99.99m, Quantity: 600)
            });

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    // ── Full valid request ────────────────────────────────────────────────────

    [Fact]
    public void ValidRequest_WithMultipleTiers_ShouldNotHaveAnyValidationErrors()
    {
        var request = new UpdateEventRequest(
            "Summer Festival", "A great event", "Hyde Park",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)),
            TotalCapacity: 500,
            PricingTiers: new List<UpdatePricingTierRequest>
            {
                new(ExistingTierId: Guid.NewGuid(), Name: "General Admission", Price: 49.99m, Quantity: 400),
                new(ExistingTierId: null,            Name: "VIP",               Price: 99.99m, Quantity: 100)
            });

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}


