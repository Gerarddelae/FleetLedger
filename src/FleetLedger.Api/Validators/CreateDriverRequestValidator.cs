using FluentValidation;

namespace FleetLedger.Api.Validators;

public class CreateDriverRequestValidator : AbstractValidator<Contracts.Requests.CreateDriverRequest>
{
    public CreateDriverRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage("LicenseNumber is required.")
            .MaximumLength(50).WithMessage("LicenseNumber must not exceed 50 characters.");

        RuleFor(x => x.LicenseCategory)
            .NotEmpty().WithMessage("LicenseCategory is required.")
            .MaximumLength(10).WithMessage("LicenseCategory must not exceed 10 characters.");

        RuleFor(x => x.LicenseExpires)
            .NotEmpty().WithMessage("LicenseExpires is required.")
            .GreaterThan(DateOnly.FromDateTime(DateTime.Today)).WithMessage("License must not be expired.");

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .Matches(@"^\+?[\d\s\-()]*$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Phone must be a valid phone number.");

        RuleFor(x => x.DepotId)
            .MaximumLength(100).WithMessage("DepotId must not exceed 100 characters.");
    }
}