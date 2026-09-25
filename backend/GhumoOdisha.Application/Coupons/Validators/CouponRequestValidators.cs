using FluentValidation;
using GhumoOdisha.Application.Coupons.Dtos;

namespace GhumoOdisha.Application.Coupons.Validators;

public class AdminCreateCouponRequestValidator : AbstractValidator<AdminCreateCouponRequest>
{
    public AdminCreateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Coupon codes can only contain letters, numbers and hyphens.");
        RuleFor(x => x.HolderName).NotEmpty().WithMessage("Enter the coupon holder's name.").MaximumLength(100);
        RuleFor(x => x.DiscountAmount).GreaterThan(0);
        RuleFor(x => x.CommissionPerSeat).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidUntil)
            .GreaterThanOrEqualTo(x => x.ValidFrom!.Value)
            .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue)
            .WithMessage("Valid-until date must be on or after the valid-from date.");
    }
}

public class AdminUpdateCouponRequestValidator : AbstractValidator<AdminUpdateCouponRequest>
{
    public AdminUpdateCouponRequestValidator()
    {
        RuleFor(x => x.HolderName).NotEmpty().WithMessage("Enter the coupon holder's name.").MaximumLength(100);
        RuleFor(x => x.DiscountAmount).GreaterThan(0);
        RuleFor(x => x.CommissionPerSeat).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidUntil)
            .GreaterThanOrEqualTo(x => x.ValidFrom!.Value)
            .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue)
            .WithMessage("Valid-until date must be on or after the valid-from date.");
    }
}

public class ValidateCouponRequestValidator : AbstractValidator<ValidateCouponRequest>
{
    public ValidateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class AddCouponPayoutRequestValidator : AbstractValidator<AddCouponPayoutRequest>
{
    public AddCouponPayoutRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.PaidAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddMinutes(5))
            .When(x => x.PaidAt.HasValue)
            .WithMessage("Payout date can't be in the future.");
    }
}
