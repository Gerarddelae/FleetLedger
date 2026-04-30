using FluentValidation;

namespace FleetLedger.Api.Validators;

public class CreateDepotRequestValidator : AbstractValidator<Contracts.Requests.CreateDepotRequest>
{
    public CreateDepotRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(200).WithMessage("City must not exceed 200 characters.");

        RuleFor(x => x.Region)
            .MaximumLength(200).When(x => x.Region != null)
            .WithMessage("Region must not exceed 200 characters.");

        RuleFor(x => x.ManagerName)
            .MaximumLength(200).When(x => x.ManagerName != null)
            .WithMessage("ManagerName must not exceed 200 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(50).When(x => x.Phone != null)
            .WithMessage("Phone must not exceed 50 characters.");
    }
}