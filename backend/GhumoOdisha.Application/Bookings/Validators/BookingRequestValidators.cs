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
        RuleFor(x => x.Method).IsInEnum().When(x => x.Method.HasValue);
        RuleFor(x => x.PaymentReference).MaximumLength(100);
    }
}

public class AddBookingPaymentRequestValidator : AbstractValidator<AddBookingPaymentRequest>
{
    public AddBookingPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method)
            .IsInEnum()
            .NotEqual(PaymentMethod.Razorpay)
            .WithMessage("Online Razorpay payments are recorded automatically — choose Cash, UPI, Bank transfer or Other.");
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.PaidAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddMinutes(5))
            .When(x => x.PaidAt.HasValue)
            .WithMessage("Payment date can't be in the future.");
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
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.WaiveRefund)
            .WithMessage("A reason is required to cancel without a refund.");
    }
}

public class ChangeSeatsRequestValidator : AbstractValidator<ChangeSeatsRequest>
{
    public ChangeSeatsRequestValidator()
    {
        RuleFor(x => x.NumberOfSeats).InclusiveBetween(1, 100);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required to change the seats.").MaximumLength(300);
    }
}

public class UpdateGenderCountsRequestValidator : AbstractValidator<UpdateGenderCountsRequest>
{
    public UpdateGenderCountsRequestValidator()
    {
        RuleFor(x => x.MaleCount).GreaterThanOrEqualTo(0).When(x => x.MaleCount.HasValue);
        RuleFor(x => x.FemaleCount).GreaterThanOrEqualTo(0).When(x => x.FemaleCount.HasValue);
    }
}

public class UpdateTravellersRequestValidator : AbstractValidator<UpdateTravellersRequest>
{
    public UpdateTravellersRequestValidator()
    {
        RuleFor(x => x.Travellers).NotNull();
        RuleForEach(x => x.Travellers).ChildRules(t =>
        {
            t.RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
            t.RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender.HasValue);
            // Only ever the last 4 digits — a full 12-digit Aadhaar number is rejected, never stored.
            t.RuleFor(x => x.AadhaarLast4).Matches("^[0-9]{4}$").When(x => !string.IsNullOrWhiteSpace(x.AadhaarLast4))
                .WithMessage("Enter only the last 4 digits of the Aadhaar number.");
            t.RuleFor(x => x.PhoneNumber).MaximumLength(15);
        });
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
