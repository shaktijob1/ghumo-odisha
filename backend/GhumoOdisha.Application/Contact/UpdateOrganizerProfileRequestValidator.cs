using FluentValidation;

namespace GhumoOdisha.Application.Contact;

public class UpdateOrganizerProfileRequestValidator : AbstractValidator<UpdateOrganizerProfileRequest>
{
    public UpdateOrganizerProfileRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Role).MaximumLength(100);
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .Matches(@"^\+?[0-9 ]{8,19}$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter the phone number with digits only, e.g. +918093731041.");
        RuleFor(x => x.Email)
            .MaximumLength(200)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Enter a valid email address.");
        RuleFor(x => x.InstagramUrl)
            .MaximumLength(300)
            .Must(BeAnInstagramLink).When(x => !string.IsNullOrWhiteSpace(x.InstagramUrl))
            .WithMessage("Enter the full Instagram link, e.g. https://www.instagram.com/ghumo__odisha/.");
        RuleFor(x => x.Quote).MaximumLength(300);
        RuleFor(x => x.PhotoCaption).MaximumLength(100);
        RuleFor(x => x.HelpTitle).MaximumLength(100);
        RuleFor(x => x.HelpText).MaximumLength(200);
    }

    private static bool BeAnInstagramLink(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".instagram.com", StringComparison.OrdinalIgnoreCase));
}
