using GhumoOdisha.Application.Bookings.Dtos;

namespace GhumoOdisha.Application.Dashboard;

public record DashboardCountersDto(
    int TotalTrips,
    int ActiveTrips,
    int TotalCustomers,
    int BookingRequests,
    int ConfirmedBookings,
    int UpcomingTripsCount,
    decimal ConfirmedRevenue,
    decimal AdvanceCollected);

public record DashboardUpcomingTripDto(int TripId, string TripTitle, DateOnly StartDate, int AvailableSeats, int TotalSeats);

public record DashboardDto(
    DashboardCountersDto Counters,
    IReadOnlyList<DashboardUpcomingTripDto> UpcomingTrips,
    IReadOnlyList<AdminBookingListItemDto> RecentRequests,
    IReadOnlyList<AdminBookingListItemDto> RecentlyConfirmed);
