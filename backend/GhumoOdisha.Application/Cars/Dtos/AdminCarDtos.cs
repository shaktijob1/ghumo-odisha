using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars.Dtos;

/// <summary>Admin list filters. "Review" = submitted and waiting for a decision.</summary>
public enum AdminCarFilter
{
    All = 0,
    Review = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
    Inactive = 5,
    /// <summary>Signed up / registered but not sent for review yet.</summary>
    Draft = 6
}

public record CarAuditEventDto(
    long CarAuditEventId,
    CarAuditEntity EntityType,
    string Action,
    string Title,
    string? OldValue,
    string? NewValue,
    string? Note,
    string ActorRole,
    int? ActorId,
    DateTime CreatedAt);

public record AdminDriverListItemDto(
    int DriverId,
    string Name,
    string? PhoneNumber,
    string? City,
    string? ProfilePhotoUrl,
    DriverStatus Status,
    string? StatusReason,
    bool AwaitingReview,
    DateTime? SubmittedForReviewAt,
    DateTime? ApprovedAt,
    int CarCount,
    int ApprovedCarCount,
    DateTime CreatedAt);

public record AdminDriverDetailDto(
    DriverProfileDto Profile,
    string? GoogleEmail,
    /// <summary>Added by an admin — can be approved without a submitted profile.</summary>
    bool AddedByAdmin,
    IReadOnlyList<AdminCarListItemDto> Cars,
    IReadOnlyList<CarAuditEventDto> History);

public record AdminCarListItemDto(
    int CarId,
    string DisplayName,
    string Category,
    string RegistrationNumber,
    int SeatCapacity,
    FuelType FuelType,
    string BaseCity,
    string? CoverPhotoUrl,
    CarStatus Status,
    string? StatusReason,
    bool AwaitingReview,
    DateTime? SubmittedForReviewAt,
    int DriverId,
    string DriverName,
    DriverStatus DriverStatus,
    bool HasPendingPricing,
    bool IsListed,
    DateTime CreatedAt);

public record AdminDriverSummaryDto(int DriverId, string Name, string? PhoneNumber, string? ProfilePhotoUrl, DriverStatus Status);

public record AdminCarDetailDto(
    DriverCarDto Car,
    AdminDriverSummaryDto Driver,
    IReadOnlyList<DriverDocumentDto> Documents,
    IReadOnlyList<CarPricingDto> PricingHistory,
    /// <summary>Why customers can't see the car right now; empty when it's listed.</summary>
    IReadOnlyList<string> NotListedReasons,
    int UpcomingBookings,
    IReadOnlyList<CarAuditEventDto> History);

public record AdminPendingPricingDto(
    int CarId,
    string CarDisplayName,
    string Category,
    string RegistrationNumber,
    CarStatus CarStatus,
    int DriverId,
    string DriverName,
    CarPricingDto Proposed,
    CarPricingDto? Current);

/// <summary>Reason is required to reject, suspend or deactivate; optional note when approving.</summary>
public record AdminDecisionRequest(string? Reason);

public record CarBookingSummaryDto(
    int CarBookingId,
    string Reference,
    string CustomerName,
    string CarDisplayName,
    string DriverName,
    DateTime PickupAt,
    CarBookingStatus Status,
    CarPaymentStatus PaymentStatus,
    decimal EstimatedTotal,
    decimal? FinalTotal,
    DateTime CreatedAt);

public record CarAdminDashboardDto(
    int TotalDrivers,
    int DriversAwaitingReview,
    int ApprovedDrivers,
    int TotalCars,
    int CarsAwaitingReview,
    int ApprovedCars,
    /// <summary>Listed in customer search right now (car, driver and pricing approved).</summary>
    int ActiveCars,
    int PricingAwaitingReview,
    int PendingApprovals,
    int UpcomingBookings,
    int TripsInProgress,
    int CompletedTrips,
    decimal BookingAmountCollected,
    decimal CompletedTripsFareTotal,
    /// <summary>Paid bookings cancelled and waiting for the admin to refund the booking amount.</summary>
    int RefundsPending,
    IReadOnlyList<CarBookingSummaryDto> RecentBookings);

/// <summary>Admin adds an owner-driver. The WhatsApp number is how the driver signs in later.</summary>
public record AdminCreateDriverRequest(string Name, string PhoneNumber, string? City);
