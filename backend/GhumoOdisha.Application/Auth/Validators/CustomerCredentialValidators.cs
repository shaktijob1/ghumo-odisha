using FluentValidation;
using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth.Validators;

public class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator()
    {
        RuleFor(x => x.Credential).NotEmpty().MaximumLength(4096);
    }
}

public class AddPhoneRequestValidator : AbstractValidator<AddPhoneRequest>
{
    public AddPhoneRequestValidator()
    {
        RuleFor(x => x.WhatsAppNumber).NotEmpty().WithMessage("WhatsApp number is required.");
    }
}
