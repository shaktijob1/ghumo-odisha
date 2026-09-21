using FluentValidation;
using GhumoOdisha.Application.Auth.Dtos;

namespace GhumoOdisha.Application.Auth.Validators;

public class RequestOtpRequestValidator : AbstractValidator<RequestOtpRequest>
{
    public RequestOtpRequestValidator()
    {
        RuleFor(x => x.WhatsAppNumber)
            .NotEmpty()
            .WithMessage("WhatsApp number is required.");

        RuleFor(x => x.Name)
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));
    }
}
