using FluentValidation;
using GhumoOdisha.Application.Destinations.Dtos;

namespace GhumoOdisha.Application.Destinations.Validators;

public class CreateDestinationRequestValidator : AbstractValidator<CreateDestinationRequest>
{
    public CreateDestinationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug can only contain lowercase letters, numbers and hyphens.");
        RuleFor(x => x.Tagline).MaximumLength(300);
        RuleFor(x => x.Region).MaximumLength(200);
        RuleFor(x => x.BestSeason).MaximumLength(100);
        RuleFor(x => x.DistanceFromBhubaneswar).MaximumLength(100);
        RuleFor(x => x.IdealDuration).MaximumLength(100);
        RuleFor(x => x.KnownFor).MaximumLength(200);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateDestinationRequestValidator : AbstractValidator<UpdateDestinationRequest>
{
    public UpdateDestinationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug can only contain lowercase letters, numbers and hyphens.");
        RuleFor(x => x.Tagline).MaximumLength(300);
        RuleFor(x => x.Region).MaximumLength(200);
        RuleFor(x => x.BestSeason).MaximumLength(100);
        RuleFor(x => x.DistanceFromBhubaneswar).MaximumLength(100);
        RuleFor(x => x.IdealDuration).MaximumLength(100);
        RuleFor(x => x.KnownFor).MaximumLength(200);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
