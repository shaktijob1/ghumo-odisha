namespace GhumoOdisha.Application.Cars.Dtos;

public enum DriverBookingScope
{
    /// <summary>Paid, not started yet.</summary>
    Upcoming = 0,
    /// <summary>Trip running.</summary>
    Active = 1,
    /// <summary>Completed or cancelled.</summary>
    History = 2
}

/// <summary>A booking as the assigned driver sees it — with the customer's contact while the trip is ahead or running.</summary>
public record DriverBookingDto(CarBookingDto Booking, string CustomerName, string? CustomerPhone, int? LastEndOdometerKm);

public record StartTripRequest(int StartOdometerKm, decimal? Latitude, decimal? Longitude);

public record EndTripRequest(
    int EndOdometerKm,
    int NightHalts,
    decimal AdditionalCharges,
    string? AdditionalChargesNote,
    decimal? Latitude,
    decimal? Longitude);

/// <summary>Server-calculated final fare shown to the driver before "Complete Trip". Nothing is saved.</summary>
public record TripFarePreviewDto(
    int StartOdometerKm,
    int EndOdometerKm,
    int ActualKm,
    /// <summary>Most night halts allowed: nights the trip has actually spanned so far.</summary>
    int MaxNightHalts,
    FareBreakdownDto Estimate,
    FareBreakdownDto Final,
    decimal BookingAmountPaid,
    decimal BalanceDue);

public record DriverEarningsDto(
    int UpcomingTrips,
    int CompletedTrips,
    int TotalKm,
    decimal TotalFare,
    decimal BookingAmountsPaidOnline,
    decimal BalanceCollected,
    decimal BalancePending,
    IReadOnlyList<CarBookingSummaryDto> RecentTrips);

/// <summary>Admin correction of a completed trip (e.g. a mistyped odometer). The reason is recorded and shown in history.</summary>
public record AdminCorrectFareRequest(
    int StartOdometerKm,
    int EndOdometerKm,
    int NightHalts,
    decimal AdditionalCharges,
    string? AdditionalChargesNote,
    string? Reason);
