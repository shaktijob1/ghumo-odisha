using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

public record CreateTripRequest(
    string Title,
    string Description,
    decimal AmountPerPerson,
    bool IncludesBreakfast,
    bool IncludesLunch,
    bool IncludesDinner,
    bool IncludesStay,
    bool IncludesCoordinator,
    IReadOnlyList<int>? DestinationIds = null,
    bool IncludesAcVehicle = false,
    bool IncludesPushbackVehicle = false,
    bool IncludesCamping = false,
    bool IncludesBonfire = false,
    bool IncludesMusicalNight = false,
    bool IncludesSwimmingPool = false,
    bool AllowCoupons = true,
    IReadOnlyList<string>? PlacesCovered = null,
    IReadOnlyList<string>? Categories = null,
    /// <summary>Both or neither: the trip's own days / nights (null = counted from each date range).</summary>
    int? DurationDays = null,
    int? DurationNights = null);

public record UpdateTripRequest(
    string Title,
    string Description,
    decimal AmountPerPerson,
    bool IncludesBreakfast,
    bool IncludesLunch,
    bool IncludesDinner,
    bool IncludesStay,
    bool IncludesCoordinator,
    TripStatus Status,
    IReadOnlyList<int>? DestinationIds = null,
    bool IncludesAcVehicle = false,
    bool IncludesPushbackVehicle = false,
    bool IncludesCamping = false,
    bool IncludesBonfire = false,
    bool IncludesMusicalNight = false,
    bool IncludesSwimmingPool = false,
    bool AllowCoupons = true,
    IReadOnlyList<string>? PlacesCovered = null,
    IReadOnlyList<string>? Categories = null,
    /// <summary>Both or neither: the trip's own days / nights (null = counted from each date range).</summary>
    int? DurationDays = null,
    int? DurationNights = null);

/// <summary>Admin: mark a whole trip Open, Booked or Closed for online bookings.</summary>
public record SetTripBookingStateRequest(TripBookingState BookingState);
