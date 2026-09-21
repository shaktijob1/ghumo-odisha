using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class AddItineraryDayRequestValidator : AbstractValidator<AddItineraryDayRequest>
{
    public AddItineraryDayRequestValidator()
    {
        RuleFor(x => x.DayNumber).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public class UpdateItineraryDayRequestValidator : AbstractValidator<UpdateItineraryDayRequest>
{
    public UpdateItineraryDayRequestValidator()
    {
        RuleFor(x => x.DayNumber).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class AddItineraryPointRequestValidator : AbstractValidator<AddItineraryPointRequest>
{
    public AddItineraryPointRequestValidator()
    {
        RuleFor(x => x.Time).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public class UpdateItineraryPointRequestValidator : AbstractValidator<UpdateItineraryPointRequest>
{
    public UpdateItineraryPointRequestValidator()
    {
        RuleFor(x => x.Time).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
