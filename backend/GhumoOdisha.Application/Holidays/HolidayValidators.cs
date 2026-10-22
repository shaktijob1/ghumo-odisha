using FluentValidation;

namespace GhumoOdisha.Application.Holidays;

public class SaveHolidayDestinationRequestValidator : AbstractValidator<SaveHolidayDestinationRequest>
{
    public SaveHolidayDestinationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the destination name.").MaximumLength(100);
        RuleFor(x => x.Tagline).MaximumLength(150);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 9999);
    }
}

public class SaveHolidayPackageRequestValidator : AbstractValidator<SaveHolidayPackageRequest>
{
    public SaveHolidayPackageRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Enter the package name.").MaximumLength(150);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.Days).InclusiveBetween(1, 60).WithMessage("Days must be between 1 and 60.");
        RuleFor(x => x.Nights).InclusiveBetween(0, 60).WithMessage("Nights must be between 0 and 60.");
        RuleFor(x => x).Must(x => x.Nights <= x.Days).WithMessage("Nights can't be more than days.");
        RuleFor(x => x.PriceFrom).InclusiveBetween(0, 9_999_999).When(x => x.PriceFrom.HasValue)
            .WithMessage("Enter a price between ₹0 and ₹99,99,999, or leave it empty for \"Price on request\".");
        RuleFor(x => x.Highlights).Must(h => h is null || h.Count <= 12).WithMessage("Add up to 12 highlights.");
        RuleForEach(x => x.Highlights).MaximumLength(120);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 9999);
    }
}
