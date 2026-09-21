using FluentValidation;
using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth.Validators;

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.WhatsAppNumber)
            .NotEmpty()
            .WithMessage("WhatsApp number is required.");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Matches("^[0-9]{4,8}$")
            .WithMessage("Enter the OTP you received.");
    }
}
