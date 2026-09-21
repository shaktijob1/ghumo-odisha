using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class AddTripHighlightRequestValidator : AbstractValidator<AddTripHighlightRequest>
{
    public AddTripHighlightRequestValidator()
    {
        RuleFor(x => x.PlaceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public class UpdateTripHighlightRequestValidator : AbstractValidator<UpdateTripHighlightRequest>
{
    public UpdateTripHighlightRequestValidator()
    {
        RuleFor(x => x.PlaceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
