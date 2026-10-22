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
        RuleFor(x => x.PricePerPerson).GreaterThan(0).When(x => x.PricePerPerson.HasValue)
            .WithMessage("The price for this date must be more than 0 (or leave it empty to use the trip's price).");
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
        RuleFor(x => x.PricePerPerson).GreaterThan(0).When(x => x.PricePerPerson.HasValue)
            .WithMessage("The price for this date must be more than 0 (or leave it empty to use the trip's price).");
    }
}

public class SetMonthPriceRequestValidator : AbstractValidator<SetMonthPriceRequest>
{
    public SetMonthPriceRequestValidator()
    {
        RuleFor(x => x.Month).Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("Choose a month.");
        RuleFor(x => x.PricePerPerson).GreaterThan(0).When(x => x.PricePerPerson.HasValue)
            .WithMessage("The price must be more than 0 (or leave it empty to use the trip's price).");
    }
}
