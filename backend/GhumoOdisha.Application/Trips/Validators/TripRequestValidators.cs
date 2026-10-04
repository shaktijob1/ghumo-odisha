using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.AmountPerPerson).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlacesCovered).Must(p => p is null || p.Count <= 60).WithMessage("Add at most 60 places covered.");
        RuleForEach(x => x.PlacesCovered).MaximumLength(80).WithMessage("Each place covered must be 80 characters or fewer.");
    }
}

public class UpdateTripRequestValidator : AbstractValidator<UpdateTripRequest>
{
    public UpdateTripRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.AmountPerPerson).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlacesCovered).Must(p => p is null || p.Count <= 60).WithMessage("Add at most 60 places covered.");
        RuleForEach(x => x.PlacesCovered).MaximumLength(80).WithMessage("Each place covered must be 80 characters or fewer.");
        RuleFor(x => x.Status).IsInEnum();
    }
}
