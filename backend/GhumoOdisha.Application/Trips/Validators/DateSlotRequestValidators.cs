using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class AddDateSlotRequestValidator : AbstractValidator<AddDateSlotRequest>
{
    public AddDateSlotRequestValidator()
    {
        RuleFor(x => x.TotalSeats).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.");
    }
}

public class UpdateDateSlotRequestValidator : AbstractValidator<UpdateDateSlotRequest>
{
    public UpdateDateSlotRequestValidator()
    {
        RuleFor(x => x.TotalSeats).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.");
        RuleFor(x => x.Status).IsInEnum();
    }
}
