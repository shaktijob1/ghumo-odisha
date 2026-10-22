using FluentValidation;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Trips.Validators;

public class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.AmountPerPerson).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlacesCovered).Must(p => p is null || p.Count <= 60).WithMessage("Add at most 60 places covered.");
        RuleForEach(x => x.PlacesCovered).MaximumLength(80).WithMessage("Each place covered must be 80 characters or fewer.");
        TripDurationRules.Apply(this, x => x.DurationDays, x => x.DurationNights);
    }
}

public class UpdateTripRequestValidator : AbstractValidator<UpdateTripRequest>
{
    public UpdateTripRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.AmountPerPerson).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlacesCovered).Must(p => p is null || p.Count <= 60).WithMessage("Add at most 60 places covered.");
        RuleForEach(x => x.PlacesCovered).MaximumLength(80).WithMessage("Each place covered must be 80 characters or fewer.");
        TripDurationRules.Apply(this, x => x.DurationDays, x => x.DurationNights);
        RuleFor(x => x.Status).IsInEnum();
    }
}

/// <summary>Days / nights are set together or not at all; a night departure can mean one more night than days.</summary>
internal static class TripDurationRules
{
    public static void Apply<T>(AbstractValidator<T> v, System.Linq.Expressions.Expression<Func<T, int?>> days, System.Linq.Expressions.Expression<Func<T, int?>> nights)
    {
        var getDays = days.Compile();
        var getNights = nights.Compile();
        v.RuleFor(days).InclusiveBetween(1, 60).When(x => getDays(x).HasValue).WithMessage("Days must be between 1 and 60.");
        v.RuleFor(nights).InclusiveBetween(0, 60).When(x => getNights(x).HasValue).WithMessage("Nights must be between 0 and 60.");
        v.RuleFor(x => x).Must(x => getDays(x).HasValue == getNights(x).HasValue)
            .WithMessage("Enter both days and nights, or leave both empty to count them from the dates.");
        v.RuleFor(x => x).Must(x => getDays(x) is not { } d || getNights(x) is not { } n || n <= d + 1)
            .WithMessage("Nights can be at most one more than days.");
    }
}
