using FluentValidation;
using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments.Validators;

public class CreatePaymentOrderRequestValidator : AbstractValidator<CreatePaymentOrderRequest>
{
    public CreatePaymentOrderRequestValidator()
    {
        RuleFor(x => x.Plan).IsInEnum();
        RuleFor(x => x.CouponCode).MaximumLength(32).When(x => x.CouponCode is not null);
    }
}

public class VerifyPaymentRequestValidator : AbstractValidator<VerifyPaymentRequest>
{
    public VerifyPaymentRequestValidator()
    {
        RuleFor(x => x.RazorpayOrderId).NotEmpty();
        RuleFor(x => x.RazorpayPaymentId).NotEmpty();
        RuleFor(x => x.RazorpaySignature).NotEmpty();
    }
}
