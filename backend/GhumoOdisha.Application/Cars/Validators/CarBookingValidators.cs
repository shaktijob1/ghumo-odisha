using FluentValidation;
using GhumoOdisha.Application.Cars.Dtos;

namespace GhumoOdisha.Application.Cars.Validators;

// Business limits (lead time, duration range, km range) are checked in CarRentalWindow / the services,
// from configuration — these are only the shape checks.

public class CarQuoteRequestValidator : AbstractValidator<CarQuoteRequest>
{
    public CarQuoteRequestValidator()
    {
        RuleFor(x => x.DurationHours).GreaterThan(0).WithMessage("Choose a duration.");
        RuleFor(x => x.Pickup).NotNull().WithMessage("Choose a pickup location.").SetValidator(new TripPlaceRequestValidator());
        RuleFor(x => x.Drop).NotNull().WithMessage("Choose your drop location.").SetValidator(new TripPlaceRequestValidator());
    }
}

public class CreateCarBookingRequestValidator : AbstractValidator<CreateCarBookingRequest>
{
    public CreateCarBookingRequestValidator()
    {
        RuleFor(x => x.CarId).GreaterThan(0);
        RuleFor(x => x.DurationHours).GreaterThan(0).WithMessage("Choose a duration.");
        RuleFor(x => x.Pickup).NotNull().WithMessage("Choose a pickup location.").SetValidator(new TripPlaceRequestValidator());
        RuleFor(x => x.Drop).NotNull().WithMessage("Choose your drop location.").SetValidator(new TripPlaceRequestValidator());
        RuleFor(x => x.PickupAddress)
            .NotEmpty().WithMessage("Enter your full pickup address.")
            .Must(a => a is null || a.Trim().Length >= 10).WithMessage("Enter your full pickup address — house / hotel, street and landmark.")
            .MaximumLength(500);
        RuleFor(x => x.CustomerNotes).MaximumLength(1000);
    }
}

public class CancelCarBookingRequestValidator : AbstractValidator<CancelCarBookingRequest>
{
    public CancelCarBookingRequestValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public class AdminCancelCarBookingRequestValidator : AbstractValidator<AdminCancelCarBookingRequest>
{
    public AdminCancelCarBookingRequestValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public class IssueCarRefundRequestValidator : AbstractValidator<IssueCarRefundRequest>
{
    public IssueCarRefundRequestValidator() =>
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Enter the refund amount.").PrecisionScale(10, 2, true);
}

public class RecordCarManualRefundRequestValidator : AbstractValidator<RecordCarManualRefundRequest>
{
    public RecordCarManualRefundRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Enter the refund amount.").PrecisionScale(10, 2, true);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(120);
    }
}

public class CarFareSearchRequestValidator : AbstractValidator<CarFareSearchRequest>
{
    public CarFareSearchRequestValidator()
    {
        RuleFor(x => x.DurationHours).GreaterThan(0).WithMessage("Choose a duration.");
        RuleFor(x => x.Pickup).NotNull().WithMessage("Choose a pickup location.").SetValidator(new TripPlaceRequestValidator());
        RuleFor(x => x.Drop).NotNull().WithMessage("Choose your drop location.").SetValidator(new TripPlaceRequestValidator());
    }
}
