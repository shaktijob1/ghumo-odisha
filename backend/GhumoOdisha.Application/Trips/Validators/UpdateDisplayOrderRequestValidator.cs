using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class UpdateDisplayOrderRequestValidator : AbstractValidator<UpdateDisplayOrderRequest>
{
    public UpdateDisplayOrderRequestValidator()
    {
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
