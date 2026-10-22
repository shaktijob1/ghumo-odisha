using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Crm.Dtos;

// ---------- Customers ----------

public record CrmCustomerFilter(
    string? Search = null,
    CrmStatus? Status = null,
    LeadSource? Source = null,
    int? TripId = null,
    int? TripDateSlotId = null,
    int? AssignedAdminUserId = null,
    int? Year = null,
    int? Month = null,
    /// <summary>Only customers with a trip interest, booking, note or contact — hides plain website sign-ups.</summary>
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 20);

public record CrmCustomerListItemDto(
    int CustomerId,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? City,
    LeadSource LeadSource,
    CrmStatus Status,
    DateTime CreatedAt,
    DateTime? LastContactAt,
    DateOnly? NextFollowUpDate,
    int? AssignedAdminUserId,
    string? AssignedAdminName,
    int InterestCount,
    int TotalBookings,
    int TotalTrips,
    decimal TotalSpent,
    /// <summary>Has signed in on the website at least once (their phone is their login).</summary>
    bool IsRegistered);

public record CrmCustomerDetailDto(
    int CustomerId,
    string Name,
    string? PhoneNumber,
    string? AlternatePhoneNumber,
    string? Email,
    bool WhatsAppAvailable,
    string? City,
    LeadSource LeadSource,
    CrmStatus Status,
    string? Summary,
    DateTime CreatedAt,
    DateTime? LastContactAt,
    DateOnly? NextFollowUpDate,
    int? AssignedAdminUserId,
    string? AssignedAdminName,
    bool IsRegistered,
    int TotalBookings,
    int TotalTrips,
    decimal TotalSpent,
    IReadOnlyList<CrmTripInterestDto> Interests,
    IReadOnlyList<CrmCustomerBookingDto> Bookings,
    IReadOnlyList<CrmFollowUpDto> FollowUps,
    IReadOnlyList<CrmNoteDto> Notes,
    IReadOnlyList<CrmCommunicationDto> Communications,
    IReadOnlyList<CrmStatusChangeDto> StatusHistory);

public record CrmCustomerBookingDto(
    int BookingId,
    string Reference,
    int TripId,
    string TripTitle,
    DateOnly StartDate,
    int Seats,
    BookingStatus Status,
    BookingSource Source,
    decimal Total,
    decimal Paid,
    decimal Balance,
    DateTime CreatedAt);

public record CrmDuplicateCheckDto(
    bool Exists,
    int? CustomerId,
    string? Name,
    string? PhoneNumber,
    CrmStatus? Status,
    int InterestCount,
    int TotalBookings,
    /// <summary>True when the number matched the customer's alternate number rather than their main one.</summary>
    bool MatchedAlternate);

public record CrmInterestInput(int TripId, int? TripDateSlotId, int Travellers, CrmStatus Status, string? Notes, decimal? ExpectedAmount);

public record CreateCrmCustomerRequest(
    string Name,
    string PhoneNumber,
    string? AlternatePhoneNumber,
    string? Email,
    bool WhatsAppAvailable,
    string? City,
    LeadSource LeadSource,
    CrmStatus Status,
    string? Summary,
    int? AssignedAdminUserId,
    /// <summary>Optional first trip interest, added with the customer.</summary>
    CrmInterestInput? Interest,
    DateOnly? NextFollowUpDate,
    string? FollowUpNote);

public record UpdateCrmCustomerRequest(
    string Name,
    string PhoneNumber,
    string? AlternatePhoneNumber,
    string? Email,
    bool WhatsAppAvailable,
    string? City,
    LeadSource LeadSource,
    string? Summary,
    int? AssignedAdminUserId);

public record ChangeCrmStatusRequest(CrmStatus Status, string? Reason);

public record AddCrmNoteRequest(string Text, int? CustomerTripInterestId);

public record CrmNoteDto(int CrmNoteId, string Text, int? CustomerTripInterestId, string? AdminName, DateTime CreatedAt);

public record CrmStatusChangeDto(
    int CrmStatusChangeId,
    int? CustomerTripInterestId,
    string? TripTitle,
    CrmStatus? FromStatus,
    CrmStatus ToStatus,
    string? Reason,
    string? AdminName,
    DateTime CreatedAt);

public record CrmAdminUserDto(int AdminUserId, string Name);

// ---------- Trip interests and pipeline ----------

public record CrmTripInterestDto(
    int CustomerTripInterestId,
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    LeadSource CustomerSource,
    int TripId,
    string TripTitle,
    int? TripDateSlotId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Travellers,
    CrmStatus Status,
    DateTime InterestedAt,
    DateTime? LastContactAt,
    DateOnly? NextFollowUpDate,
    string? Notes,
    decimal? ExpectedAmount,
    int? BookingId,
    string? BookingReference,
    decimal? BookingTotal,
    decimal? BookingPaid,
    string? LostReason);

public record CrmTripInterestFilter(
    int? TripId = null,
    int? TripDateSlotId = null,
    CrmStatus? Status = null,
    LeadSource? Source = null,
    int? Year = null,
    int? Month = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

public record CreateTripInterestRequest(
    int CustomerId,
    int TripId,
    int? TripDateSlotId,
    int Travellers,
    CrmStatus Status,
    string? Notes,
    decimal? ExpectedAmount,
    DateOnly? NextFollowUpDate);

public record UpdateTripInterestRequest(int TripId, int? TripDateSlotId, int Travellers, string? Notes, decimal? ExpectedAmount, string? LostReason);

public record CrmPipelineColumnDto(CrmStatus Status, int Count, IReadOnlyList<CrmTripInterestDto> Items);

public record CrmPipelineDto(IReadOnlyList<CrmPipelineColumnDto> Columns);

// ---------- Follow-ups ----------

public enum CrmFollowUpBucket
{
    Today = 0,
    Overdue = 1,
    Upcoming = 2,
    /// <summary>Every pending follow-up, any date.</summary>
    Pending = 3,
    Done = 4
}

public record CrmFollowUpFilter(
    CrmFollowUpBucket Bucket = CrmFollowUpBucket.Today,
    int? AssignedAdminUserId = null,
    int? TripId = null,
    int? CustomerId = null,
    int Page = 1,
    int PageSize = 50);

public record CrmFollowUpDto(
    int CrmFollowUpId,
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    CrmStatus CustomerStatus,
    int? CustomerTripInterestId,
    string? TripTitle,
    DateOnly? DepartureDate,
    CrmStatus? InterestStatus,
    DateOnly DueDate,
    /// <summary>Days past due (0 when due today or later).</summary>
    int DaysOverdue,
    string? Note,
    CrmFollowUpStatus Status,
    string? Outcome,
    DateTime? CompletedAt,
    int? AssignedAdminUserId,
    string? AssignedAdminName);

public record CrmFollowUpSummaryDto(int Today, int Overdue, int Upcoming);

public record CreateFollowUpRequest(int CustomerId, int? CustomerTripInterestId, DateOnly DueDate, string? Note, int? AssignedAdminUserId);

/// <summary>Marks a follow-up done; optionally schedules the next one and moves the stage on.</summary>
public record CompleteFollowUpRequest(string? Outcome, DateOnly? NextDueDate, string? NextNote, CrmStatus? NewStatus);

public record RescheduleFollowUpRequest(DateOnly DueDate, string? Note);

// ---------- WhatsApp templates and messages ----------

public record CrmTemplateDto(int CrmWhatsAppTemplateId, string Name, CrmTemplateCategory Category, string Body, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public record SaveCrmTemplateRequest(string Name, CrmTemplateCategory Category, string Body, bool IsActive);

/// <summary>Fills a template (or a typed message) for one customer and, optionally, one of their trip interests.</summary>
public record RenderCrmMessageRequest(int CustomerId, int? CustomerTripInterestId, int? CrmWhatsAppTemplateId, string? Body);

public record CrmRenderedMessageDto(
    string Message,
    string? TemplateName,
    string CustomerName,
    /// <summary>International digits for wa.me ("919876543210"); null when the customer has no usable mobile.</summary>
    string? WhatsAppPhone,
    bool WhatsAppAvailable,
    /// <summary>Placeholders that had no value for this customer/trip and were left blank.</summary>
    IReadOnlyList<string> MissingValues);

/// <summary>The admin is about to open WhatsApp with <see cref="Message"/> (as finally edited in the preview).</summary>
public record OpenWhatsAppRequest(int CustomerId, int? CustomerTripInterestId, int? CrmWhatsAppTemplateId, string Message);

public record CrmWhatsAppLinkDto(int CrmCommunicationId, string AppUrl, string WebUrl);

// ---------- Communication history ----------

public record CrmCommunicationFilter(
    int? CustomerId = null,
    /// <summary>Customer name or mobile number.</summary>
    string? Search = null,
    int? TripId = null,
    CrmChannel? Channel = null,
    CrmMessageStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int? AdminUserId = null,
    int Page = 1,
    int PageSize = 50);

public record CrmCommunicationDto(
    int CrmCommunicationId,
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    int? CustomerTripInterestId,
    string? TripTitle,
    CrmChannel Channel,
    CrmMessageStatus Status,
    string Action,
    string? TemplateName,
    string? MessagePreview,
    string? AdminName,
    DateTime CreatedAt,
    DateTime? SentConfirmedAt);

/// <summary>A call, email or other contact the admin records by hand.</summary>
public record LogCommunicationRequest(int CustomerId, int? CustomerTripInterestId, CrmChannel Channel, string Action, string? Details);

// ---------- Departures ----------

/// <summary>Badge for a departure, worked out from seats, dates and the CRM rules — never stored.</summary>
public enum CrmDepartureStatus
{
    /// <summary>Hidden from the website (the departure is switched off).</summary>
    Draft = 0,
    OpenForBooking = 1,
    FillingFast = 2,
    /// <summary>The minimum number of booked seats has been reached.</summary>
    Confirmed = 3,
    AlmostFull = 4,
    SoldOut = 5,
    Completed = 6
}

public record CrmDepartureFilter(int? TripId = null, int? Year = null, int? Month = null, bool IncludePast = false, int Page = 1, int PageSize = 50);

public record CrmDepartureDto(
    int TripDateSlotId,
    int TripId,
    string TripTitle,
    DateOnly StartDate,
    DateOnly EndDate,
    TripDateSlotStatus SlotStatus,
    string? Vehicle,
    decimal RegularPrice,
    decimal? PricePerPerson,
    decimal? EarlyBirdPrice,
    DateOnly? EarlyBirdEndsOn,
    decimal CurrentPrice,
    bool IsEarlyBird,
    decimal BookingAmountPerSeat,
    int TotalSeats,
    int BookedSeats,
    int AvailableSeats,
    int? MinimumBookedSeats,
    bool MinimumReached,
    CrmDepartureStatus CampaignStatus,
    int Leads,
    int Interested,
    int HotLeads,
    int BookingPending,
    int Booked,
    int Cancelled);

public record CrmDepartureBookingDto(
    int BookingId,
    string Reference,
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    LeadSource CustomerSource,
    int Seats,
    BookingStatus Status,
    BookingSource Source,
    decimal Total,
    decimal Paid,
    decimal Balance,
    DateTime CreatedAt);

public record CrmDepartureDetailDto(CrmDepartureDto Departure, IReadOnlyList<CrmTripInterestDto> Interests, IReadOnlyList<CrmDepartureBookingDto> Bookings);

public record CreateCrmDepartureRequest(
    int TripId,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalSeats,
    /// <summary>False keeps it hidden from the website (Draft).</summary>
    bool OpenForBooking,
    string? Vehicle,
    int? MinimumBookedSeats,
    decimal? PricePerPerson,
    decimal? EarlyBirdPrice,
    DateOnly? EarlyBirdEndsOn);

/// <summary>CRM details only — dates, seats and on/off still go through Admin → Trips (which locks the seat row).</summary>
public record UpdateCrmDepartureDetailsRequest(
    string? Vehicle,
    int? MinimumBookedSeats,
    decimal? PricePerPerson,
    decimal? EarlyBirdPrice,
    DateOnly? EarlyBirdEndsOn);

// ---------- Dashboard and reports ----------

public record CrmDashboardFilter(int? Year = null, int? Month = null, int? TripId = null, int? TripDateSlotId = null, LeadSource? Source = null, CrmStatus? Status = null);

public record CrmDashboardDto(
    int TotalCustomers,
    int TotalLeads,
    int NewLeads,
    int Interested,
    int HotLeads,
    int FollowUp,
    int BookingPending,
    int Booked,
    int Completed,
    int Cancelled,
    int NotInterested,
    int Later,
    int RepeatCustomers,
    int FollowUpsToday,
    int FollowUpsOverdue,
    int FollowUpsUpcoming,
    /// <summary>Booked + completed leads as a percentage of all leads in the filter.</summary>
    decimal ConversionRate,
    IReadOnlyList<CrmFollowUpDto> TodayFollowUps,
    IReadOnlyList<CrmDepartureDto> UpcomingDepartures);

public record CrmSourceRowDto(LeadSource Source, int Leads, int Bookings, decimal BookedValue, decimal ConversionRate);

public record CrmSourceReportDto(DateOnly? From, DateOnly? To, IReadOnlyList<CrmSourceRowDto> Rows);

public record CrmTopTripDto(int TripId, string TripTitle, int Bookings, int Seats, decimal BookedValue);

public record CrmMonthlyReportDto(
    int Year,
    int Month,
    int TotalLeads,
    int TotalBookings,
    int SeatsBooked,
    decimal BookedValue,
    /// <summary>Money actually received in the month (all payments recorded, before refunds).</summary>
    decimal PaymentsReceived,
    decimal ConversionRate,
    int Cancelled,
    int Completed,
    IReadOnlyList<CrmTopTripDto> TopTrips,
    IReadOnlyList<CrmSourceRowDto> TopSources);

// ---------- CSV import ----------

/// <summary>One row of an imported file, as the browser read it (all text, all optional).</summary>
public record CrmImportRow(
    string? Name,
    string? Mobile,
    string? Email,
    string? City,
    string? Trip,
    string? Departure,
    string? Status,
    string? Source,
    string? Notes,
    int? Travellers);

/// <param name="AddInterestToExisting">For rows whose mobile is already a customer: add the row's trip to them (else skip the row).</param>
public record CrmImportRequest(IReadOnlyList<CrmImportRow> Rows, bool AddInterestToExisting);

public enum CrmImportOutcome
{
    New = 0,
    /// <summary>The mobile is already a customer — never a second customer.</summary>
    Existing = 1,
    /// <summary>The same mobile appeared on an earlier row of the file.</summary>
    SameAsEarlierRow = 2,
    Invalid = 3
}

public record CrmImportRowResult(
    int Row,
    CrmImportOutcome Outcome,
    string? Name,
    string? Mobile,
    int? ExistingCustomerId,
    string? ExistingCustomerName,
    int? TripId,
    string? TripTitle,
    int? TripDateSlotId,
    CrmStatus Status,
    LeadSource Source,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public record CrmImportPreviewDto(int Total, int New, int Existing, int Invalid, IReadOnlyList<CrmImportRowResult> Rows);

public record CrmImportResultDto(int CustomersCreated, int InterestsAdded, int Skipped, int Invalid, IReadOnlyList<string> Errors);
