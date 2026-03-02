using FluentValidation;
using TicketService.Application.Events.Commands;

namespace TicketService.Application.Events.Validators;

public class CreateEventValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Event name is required.")
            .MaximumLength(200).WithMessage("Event name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotNull().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Venue)
            .NotEmpty().WithMessage("Venue is required.")
            .MaximumLength(300).WithMessage("Venue must not exceed 300 characters.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Event date is required.")
            .Must(date => date > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Event date must be in the future.");

        RuleFor(x => x.Time)
            .NotEmpty().WithMessage("Event time is required.");

        RuleFor(x => x.TotalCapacity)
            .GreaterThan(0).WithMessage("Total capacity must be greater than zero.");

        RuleFor(x => x.PricingTiers)
            .NotEmpty().WithMessage("At least one pricing tier is required.");

        RuleForEach(x => x.PricingTiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.Name)
                .NotEmpty().WithMessage("Tier name is required.")
                .MaximumLength(100).WithMessage("Tier name must not exceed 100 characters.");

            tier.RuleFor(t => t.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Tier price must be zero or greater.");

            tier.RuleFor(t => t.Quantity)
                .GreaterThan(0).WithMessage("Tier quantity must be greater than zero.");
        });

        // Cross-field rule: sum of tier quantities must equal TotalCapacity
        RuleFor(x => x)
            .Must(x => x.PricingTiers != null &&
                       x.PricingTiers.Sum(t => t.Quantity) == x.TotalCapacity)
            .WithMessage("The sum of all pricing tier quantities must equal the total capacity.")
            .When(x => x.PricingTiers != null && x.PricingTiers.Any() && x.TotalCapacity > 0);
    }
}


