using FluentValidation;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings.Validators;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.TripDateSlotId).GreaterThan(0);
        RuleFor(x => x.NumberOfSeats).GreaterThan(0);
        RuleFor(x => x.CustomerNotes).MaximumLength(1000);
    }
}

public class ConfirmBookingRequestValidator : AbstractValidator<ConfirmBookingRequest>
{
    public ConfirmBookingRequestValidator()
    {
        RuleFor(x => x.AdvanceAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}

public class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequest>
{
    public RejectBookingRequestValidator()
    {
        RuleFor(x => x.AdminNotes).MaximumLength(1000);
    }
}

public class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.AdminNotes).MaximumLength(1000);
    }
}

public class CreateManualBookingRequestValidator : AbstractValidator<CreateManualBookingRequest>
{
    public CreateManualBookingRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || !string.IsNullOrWhiteSpace(x.NewCustomerName))
            .WithMessage("Either an existing customer or a new customer name and phone number is required.");

        RuleFor(x => x.NewCustomerPhoneNumber)
            .Matches("^[6-9][0-9]{9}$")
            .When(x => !x.CustomerId.HasValue)
            .WithMessage("Enter a valid 10-digit phone number.");

        RuleFor(x => x.NewCustomerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.NewCustomerEmail));

        RuleFor(x => x.TripId).GreaterThan(0);
        RuleFor(x => x.TripDateSlotId).GreaterThan(0);
        RuleFor(x => x.NumberOfSeats).GreaterThan(0);
        RuleFor(x => x.AdvanceAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialStatus)
            .Must(s => s is BookingStatus.Requested or BookingStatus.Pending)
            .WithMessage("A booking can only be created as Requested or Pending — confirming requires the confirm action.");
        RuleFor(x => x.AdminNotes).MaximumLength(1000);
    }
}
