using FluentValidation;

namespace GhumoOdisha.Application.SearchLogs;

public class RecordSearchRequestValidator : AbstractValidator<RecordSearchRequest>
{
    public RecordSearchRequestValidator()
    {
        RuleFor(r => r.Month)
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("Month must be in yyyy-MM format.")
            .When(r => !string.IsNullOrWhiteSpace(r.Month));
        RuleFor(r => r.Place).MaximumLength(100);
        RuleFor(r => r.ResultCount).InclusiveBetween(0, 10000);
        RuleFor(r => r)
            .Must(r => !string.IsNullOrWhiteSpace(r.Month) || !string.IsNullOrWhiteSpace(r.Place))
            .WithMessage("Choose a month or a place to search.");
    }
}
