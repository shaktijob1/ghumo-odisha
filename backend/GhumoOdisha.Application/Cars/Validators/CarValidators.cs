using FluentValidation;
using GhumoOdisha.Application.Cars.Dtos;

namespace GhumoOdisha.Application.Cars.Validators;

public class UpdateDriverProfileRequestValidator : AbstractValidator<UpdateDriverProfileRequest>
{
    public UpdateDriverProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your full name.").MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.DrivingLicenceNumber)
            .Matches("^[A-Za-z0-9 -]{8,25}$").WithMessage("Enter a valid driving licence number.")
            .When(x => !string.IsNullOrWhiteSpace(x.DrivingLicenceNumber));
        RuleFor(x => x.LicenceExpiryDate)
            .Must(d => d is null || d > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Your driving licence has expired.");
        RuleFor(x => x.ExperienceYears).InclusiveBetween(0, 60).When(x => x.ExperienceYears is not null);
    }
}

public class SaveCarRequestValidator : AbstractValidator<SaveCarRequest>
{
    public SaveCarRequestValidator()
    {
        RuleFor(x => x.Brand).NotEmpty().WithMessage("Enter the car's brand.").MaximumLength(60);
        RuleFor(x => x.ModelName).NotEmpty().WithMessage("Enter the car's model.").MaximumLength(100);
        RuleFor(x => x.RegistrationNumber).NotEmpty().WithMessage("Enter the registration number.").MaximumLength(20);
        RuleFor(x => x.FuelType).IsInEnum().WithMessage("Choose a fuel type.");
        RuleFor(x => x.SeatCapacity).Must(s => CarRules.SeatCapacities.Contains(s))
            .WithMessage($"Seats must be one of {string.Join(", ", CarRules.SeatCapacities)}.");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.BaseCity).NotEmpty().WithMessage("Enter the city the car is based in.").MaximumLength(100);
    }
}

public class SubmitPricingRequestValidator : AbstractValidator<SubmitPricingRequest>
{
    public SubmitPricingRequestValidator()
    {
        RuleFor(x => x.PricePerKm).InclusiveBetween(1m, 500m).WithMessage("Price per km must be between ₹1 and ₹500.")
            .PrecisionScale(10, 2, true);
        RuleFor(x => x.NightHaltPrice).InclusiveBetween(0m, 10000m).WithMessage("Night halt must be between ₹0 and ₹10,000.")
            .PrecisionScale(10, 2, true);
        RuleFor(x => x.Tiers).NotNull().NotEmpty().WithMessage("Add at least one base fare.");
        RuleForEach(x => x.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.UpToKm).InclusiveBetween(1, 5000).When(t => t.UpToKm is not null)
                .WithMessage("Distance limits must be between 1 and 5,000 km.");
            tier.RuleFor(t => t.BaseFare).InclusiveBetween(0m, 100000m).WithMessage("Base fare must be between ₹0 and ₹1,00,000.")
                .PrecisionScale(10, 2, true);
        });
    }
}

public class AdminCreateDriverRequestValidator : AbstractValidator<AdminCreateDriverRequest>
{
    public AdminCreateDriverRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the driver's full name.").MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Enter the driver's WhatsApp number.").MaximumLength(20);
        RuleFor(x => x.City).MaximumLength(100);
    }
}
