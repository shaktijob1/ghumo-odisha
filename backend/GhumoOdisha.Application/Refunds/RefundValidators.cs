using FluentValidation;
using GhumoOdisha.Application.Refunds.Dtos;

namespace GhumoOdisha.Application.Refunds;

public class IssueRazorpayRefundRequestValidator : AbstractValidator<IssueRazorpayRefundRequest>
{
    public IssueRazorpayRefundRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Refund amount must be more than zero.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public class RecordManualRefundRequestValidator : AbstractValidator<RecordManualRefundRequest>
{
    public RecordManualRefundRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Refund amount must be more than zero.");
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public class SettleRefundRequestValidator : AbstractValidator<SettleRefundRequest>
{
    public SettleRefundRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(150);
    }
}
