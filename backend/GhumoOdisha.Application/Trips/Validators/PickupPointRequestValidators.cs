using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class AddPickupPointRequestValidator : AbstractValidator<AddPickupPointRequest>
{
    public AddPickupPointRequestValidator()
    {
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Time).NotEmpty().MaximumLength(50);
    }
}

public class UpdatePickupPointRequestValidator : AbstractValidator<UpdatePickupPointRequest>
{
    public UpdatePickupPointRequestValidator()
    {
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Time).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
