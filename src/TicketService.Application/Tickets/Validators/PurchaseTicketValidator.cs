using FluentValidation;
using TicketService.Application.Tickets.Commands;

namespace TicketService.Application.Tickets.Validators;

public class PurchaseTicketValidator : AbstractValidator<PurchaseTicketRequest>
{
    public PurchaseTicketValidator()
    {
        RuleFor(x => x.PricingTierId)
            .NotEmpty().WithMessage("Pricing tier ID is required.");

        RuleFor(x => x.PurchaserName)
            .NotEmpty().WithMessage("Purchaser name is required.")
            .MaximumLength(200).WithMessage("Purchaser name must not exceed 200 characters.");

        RuleFor(x => x.PurchaserEmail)
            .NotEmpty().WithMessage("Purchaser email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(320).WithMessage("Email address must not exceed 320 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be at least 1.")
            .LessThanOrEqualTo(10).WithMessage("A maximum of 10 tickets can be purchased per transaction.");
    }
}


