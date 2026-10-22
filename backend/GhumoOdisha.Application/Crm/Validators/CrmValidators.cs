using FluentValidation;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Crm.Validators;

/// <summary>Stages that follow real bookings (or describe the whole customer) and so can't be picked for a new enquiry.</summary>
internal static class CrmStages
{
    public static bool IsEnquiryStage(CrmStatus status) =>
        status is not (CrmStatus.Booked or CrmStatus.Completed or CrmStatus.RepeatCustomer);

    public const string EnquiryStageMessage = "Booked, Completed and Repeat customer follow real bookings — pick an enquiry stage (e.g. Interested or Hot lead).";
}

public class CrmInterestInputValidator : AbstractValidator<CrmInterestInput>
{
    public CrmInterestInputValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0).WithMessage("Choose a trip.");
        RuleFor(x => x.TripDateSlotId).GreaterThan(0).When(x => x.TripDateSlotId.HasValue);
        RuleFor(x => x.Travellers).InclusiveBetween(1, 100).WithMessage("Travellers must be between 1 and 100.");
        RuleFor(x => x.Status).IsInEnum().Must(CrmStages.IsEnquiryStage).WithMessage(CrmStages.EnquiryStageMessage);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ExpectedAmount).InclusiveBetween(0, 10_000_000).When(x => x.ExpectedAmount.HasValue);
    }
}

public class CreateCrmCustomerRequestValidator : AbstractValidator<CreateCrmCustomerRequest>
{
    public CreateCrmCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the customer's name.").MaximumLength(150);
        RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Enter the customer's mobile number.")
            .Must(p => CrmPhone.Normalize(p) is not null).WithMessage("Enter a valid 10-digit Indian mobile number.");
        RuleFor(x => x.AlternatePhoneNumber).Must(p => CrmPhone.Normalize(p) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.AlternatePhoneNumber)).WithMessage("Enter a valid alternate mobile number, or leave it empty.");
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.LeadSource).IsInEnum();
        RuleFor(x => x.Status).IsInEnum().Must(CrmStages.IsEnquiryStage).WithMessage(CrmStages.EnquiryStageMessage);
        RuleFor(x => x.Summary).MaximumLength(2000);
        RuleFor(x => x.Interest!).SetValidator(new CrmInterestInputValidator()).When(x => x.Interest is not null);
        RuleFor(x => x.FollowUpNote).MaximumLength(1000);
    }
}

public class UpdateCrmCustomerRequestValidator : AbstractValidator<UpdateCrmCustomerRequest>
{
    public UpdateCrmCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the customer's name.").MaximumLength(150);
        RuleFor(x => x.PhoneNumber).NotEmpty().Must(p => CrmPhone.Normalize(p) is not null).WithMessage("Enter a valid 10-digit Indian mobile number.");
        RuleFor(x => x.AlternatePhoneNumber).Must(p => CrmPhone.Normalize(p) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.AlternatePhoneNumber)).WithMessage("Enter a valid alternate mobile number, or leave it empty.");
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.LeadSource).IsInEnum();
        RuleFor(x => x.Summary).MaximumLength(2000);
    }
}

public class ChangeCrmStatusRequestValidator : AbstractValidator<ChangeCrmStatusRequest>
{
    public ChangeCrmStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class AddCrmNoteRequestValidator : AbstractValidator<AddCrmNoteRequest>
{
    public AddCrmNoteRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Write the note.").MaximumLength(2000);
    }
}

public class CreateTripInterestRequestValidator : AbstractValidator<CreateTripInterestRequest>
{
    public CreateTripInterestRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.TripId).GreaterThan(0).WithMessage("Choose a trip.");
        RuleFor(x => x.TripDateSlotId).GreaterThan(0).When(x => x.TripDateSlotId.HasValue);
        RuleFor(x => x.Travellers).InclusiveBetween(1, 100).WithMessage("Travellers must be between 1 and 100.");
        RuleFor(x => x.Status).IsInEnum().Must(CrmStages.IsEnquiryStage).WithMessage(CrmStages.EnquiryStageMessage);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ExpectedAmount).InclusiveBetween(0, 10_000_000).When(x => x.ExpectedAmount.HasValue);
    }
}

public class UpdateTripInterestRequestValidator : AbstractValidator<UpdateTripInterestRequest>
{
    public UpdateTripInterestRequestValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0).WithMessage("Choose a trip.");
        RuleFor(x => x.TripDateSlotId).GreaterThan(0).When(x => x.TripDateSlotId.HasValue);
        RuleFor(x => x.Travellers).InclusiveBetween(1, 100).WithMessage("Travellers must be between 1 and 100.");
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ExpectedAmount).InclusiveBetween(0, 10_000_000).When(x => x.ExpectedAmount.HasValue);
        RuleFor(x => x.LostReason).MaximumLength(500);
    }
}

public class CreateFollowUpRequestValidator : AbstractValidator<CreateFollowUpRequest>
{
    public CreateFollowUpRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.DueDate).NotEmpty().WithMessage("Choose the follow-up date.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class CompleteFollowUpRequestValidator : AbstractValidator<CompleteFollowUpRequest>
{
    public CompleteFollowUpRequestValidator()
    {
        RuleFor(x => x.Outcome).MaximumLength(1000);
        RuleFor(x => x.NextNote).MaximumLength(1000);
        RuleFor(x => x.NewStatus).IsInEnum().When(x => x.NewStatus.HasValue);
    }
}

public class RescheduleFollowUpRequestValidator : AbstractValidator<RescheduleFollowUpRequest>
{
    public RescheduleFollowUpRequestValidator()
    {
        RuleFor(x => x.DueDate).NotEmpty().WithMessage("Choose the new date.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class SaveCrmTemplateRequestValidator : AbstractValidator<SaveCrmTemplateRequest>
{
    public SaveCrmTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name the template.").MaximumLength(100);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write the message.").MaximumLength(2000);
    }
}

public class RenderCrmMessageRequestValidator : AbstractValidator<RenderCrmMessageRequest>
{
    public RenderCrmMessageRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.CrmWhatsAppTemplateId.HasValue || !string.IsNullOrWhiteSpace(x.Body))
            .WithMessage("Choose a template or type a message.");
        RuleFor(x => x.Body).MaximumLength(4000);
    }
}

public class OpenWhatsAppRequestValidator : AbstractValidator<OpenWhatsAppRequest>
{
    public OpenWhatsAppRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Message).NotEmpty().WithMessage("The message is empty.").MaximumLength(4000);
    }
}

public class LogCommunicationRequestValidator : AbstractValidator<LogCommunicationRequest>
{
    public LogCommunicationRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Action).NotEmpty().WithMessage("Say what happened (e.g. \"Called — no answer\").").MaximumLength(200);
        RuleFor(x => x.Details).MaximumLength(4000);
    }
}

public class CreateCrmDepartureRequestValidator : AbstractValidator<CreateCrmDepartureRequest>
{
    public CreateCrmDepartureRequestValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0).WithMessage("Choose a trip.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("The return date can't be before the departure date.");
        RuleFor(x => x.TotalSeats).InclusiveBetween(1, 500).WithMessage("Seats must be between 1 and 500.");
        RuleFor(x => x.Vehicle).MaximumLength(100);
        RuleFor(x => x.PricePerPerson).InclusiveBetween(1, 1_000_000).When(x => x.PricePerPerson.HasValue);
        RuleFor(x => x.EarlyBirdPrice).InclusiveBetween(1, 1_000_000).When(x => x.EarlyBirdPrice.HasValue);
    }
}

public class UpdateCrmDepartureDetailsRequestValidator : AbstractValidator<UpdateCrmDepartureDetailsRequest>
{
    public UpdateCrmDepartureDetailsRequestValidator()
    {
        RuleFor(x => x.Vehicle).MaximumLength(100);
        RuleFor(x => x.PricePerPerson).InclusiveBetween(1, 1_000_000).When(x => x.PricePerPerson.HasValue);
        RuleFor(x => x.EarlyBirdPrice).InclusiveBetween(1, 1_000_000).When(x => x.EarlyBirdPrice.HasValue);
    }
}

public class CrmImportRequestValidator : AbstractValidator<CrmImportRequest>
{
    public CrmImportRequestValidator()
    {
        RuleFor(x => x.Rows).NotNull().Must(r => r.Count is > 0 and <= CrmImportService.MaxRows)
            .WithMessage($"Import between 1 and {CrmImportService.MaxRows} rows at a time.");
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.Name).MaximumLength(300);
            row.RuleFor(r => r.Mobile).MaximumLength(40);
            row.RuleFor(r => r.Email).MaximumLength(300);
            row.RuleFor(r => r.City).MaximumLength(200);
            row.RuleFor(r => r.Trip).MaximumLength(300);
            row.RuleFor(r => r.Departure).MaximumLength(40);
            row.RuleFor(r => r.Status).MaximumLength(60);
            row.RuleFor(r => r.Source).MaximumLength(60);
            row.RuleFor(r => r.Notes).MaximumLength(4000);
        });
    }
}
