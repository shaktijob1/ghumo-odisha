using FluentValidation;
using GhumoOdisha.Application.Coupons.Dtos;

namespace GhumoOdisha.Application.Coupons.Validators;

public class AdminCreateCouponRequestValidator : AbstractValidator<AdminCreateCouponRequest>
{
    public AdminCreateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Coupon codes can only contain letters, numbers and hyphens.");
        RuleFor(x => x.DiscountAmount).GreaterThan(0);
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
        RuleFor(x => x.DiscountAmount).GreaterThan(0);
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
