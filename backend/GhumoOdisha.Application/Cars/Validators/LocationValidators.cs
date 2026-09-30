using FluentValidation;
using GhumoOdisha.Application.Cars.Dtos;

namespace GhumoOdisha.Application.Cars.Validators;

public class TripPlaceRequestValidator : AbstractValidator<TripPlaceRequest>
{
    public TripPlaceRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Label).NotEmpty().WithMessage("Choose a place from the suggestions.").MaximumLength(300);
    }
}

public class SaveServiceAreaRequestValidator : AbstractValidator<SaveServiceAreaRequest>
{
    public SaveServiceAreaRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the area a name.").MaximumLength(100);
        RuleFor(x => x)
            .Must(x => (x.Boundary?.Count ?? 0) > 0 || (x.Pincodes?.Count ?? 0) > 0)
            .WithMessage("Draw a zone on the map or add at least one PIN code.");
        RuleFor(x => x.Boundary)
            .Must(b => b is null || b.Count == 0 || b.Count >= 3).WithMessage("A zone needs at least 3 corners.")
            .Must(b => b is null || b.Count <= 200).WithMessage("A zone can have at most 200 corners.");
        RuleForEach(x => x.Boundary).ChildRules(p =>
        {
            p.RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
            p.RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        });
        RuleFor(x => x.Pincodes).Must(p => p is null || p.Count <= 300).WithMessage("Use at most 300 PIN codes per area.");
        RuleForEach(x => x.Pincodes)
            .Matches(@"^\s*[1-9][0-9]{5}\s*$").WithMessage("'{PropertyValue}' isn't a 6-digit PIN code.");
    }
}

public class PickupCheckRequestValidator : AbstractValidator<PickupCheckRequest>
{
    public PickupCheckRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class SetBaseLocationRequestValidator : AbstractValidator<SetBaseLocationRequest>
{
    public SetBaseLocationRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Label).NotEmpty().WithMessage("Choose the starting point on the map.").MaximumLength(300);
    }
}
