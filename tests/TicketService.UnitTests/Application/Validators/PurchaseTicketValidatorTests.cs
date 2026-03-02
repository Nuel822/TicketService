using FluentValidation.TestHelper;
using TicketService.Application.Tickets.Commands;
using TicketService.Application.Tickets.Validators;

namespace TicketService.UnitTests.Application.Validators;

public class PurchaseTicketValidatorTests
{
    private readonly PurchaseTicketValidator _validator = new();

    private static PurchaseTicketRequest ValidRequest() =>
        new(
            PricingTierId: Guid.NewGuid(),
            PurchaserName: "Alice Smith",
            PurchaserEmail: "alice@example.com",
            Quantity: 2);

    // ── PricingTierId ─────────────────────────────────────────────────────────

    [Fact]
    public void PricingTierId_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PricingTierId = Guid.Empty };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PricingTierId);
    }

    // ── PurchaserName ─────────────────────────────────────────────────────────

    [Fact]
    public void PurchaserName_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PurchaserName = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PurchaserName);
    }

    [Fact]
    public void PurchaserName_WhenExceeds200Chars_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PurchaserName = new string('A', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PurchaserName);
    }

    // ── PurchaserEmail ────────────────────────────────────────────────────────

    [Fact]
    public void PurchaserEmail_WhenEmpty_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PurchaserEmail = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PurchaserEmail);
    }

    [Fact]
    public void PurchaserEmail_WhenInvalidFormat_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { PurchaserEmail = "not-an-email" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PurchaserEmail);
    }

    // ── Quantity ──────────────────────────────────────────────────────────────

    [Fact]
    public void Quantity_WhenZero_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Quantity = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_WhenNegative_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Quantity = -1 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_WhenExceeds10_ShouldHaveValidationError()
    {
        var request = ValidRequest() with { Quantity = 11 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_WhenExactly10_ShouldNotHaveValidationError()
    {
        var request = ValidRequest() with { Quantity = 10 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
    }

    // ── Full valid request ────────────────────────────────────────────────────

    [Fact]
    public void ValidRequest_ShouldNotHaveAnyValidationErrors()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }
}


