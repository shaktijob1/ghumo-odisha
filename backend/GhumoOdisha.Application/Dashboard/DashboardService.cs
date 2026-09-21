using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Dashboard;

public class DashboardService(IGhumoOdishaDbContext db) : IDashboardService
{
    private const int RecentItemsLimit = 5;

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalTrips = await db.Trips.CountAsync(cancellationToken);
        var activeTrips = await db.Trips.CountAsync(t => t.Status == TripStatus.Active, cancellationToken);
        var totalCustomers = await db.Customers.CountAsync(cancellationToken);
        var bookingRequests = await db.Bookings.CountAsync(b => b.BookingStatus == BookingStatus.Requested || b.BookingStatus == BookingStatus.Pending, cancellationToken);
        var confirmedBookings = await db.Bookings.CountAsync(b => b.BookingStatus == BookingStatus.Confirmed, cancellationToken);
        var upcomingTripsCount = await db.TripDateSlots
            .Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= today)
            .Select(s => s.TripId)
            .Distinct()
            .CountAsync(cancellationToken);

        var revenueStatuses = new List<BookingStatus> { BookingStatus.Confirmed, BookingStatus.Completed };
        var confirmedRevenue = await db.Bookings
            .Where(b => revenueStatuses.Contains(b.BookingStatus))
            .SumAsync(b => (decimal?)b.TotalAmount, cancellationToken) ?? 0;
        var advanceCollected = await db.Bookings
            .Where(b => revenueStatuses.Contains(b.BookingStatus))
            .SumAsync(b => (decimal?)b.AdvanceAmount, cancellationToken) ?? 0;

        var counters = new DashboardCountersDto(
            totalTrips, activeTrips, totalCustomers, bookingRequests,
            confirmedBookings, upcomingTripsCount, confirmedRevenue, advanceCollected);

        var upcomingTrips = await db.TripDateSlots
            .Include(s => s.Trip)
            .Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= today)
            .OrderBy(s => s.StartDate)
            .Take(RecentItemsLimit)
            .Select(s => new DashboardUpcomingTripDto(s.TripId, s.Trip.Title, s.StartDate, s.AvailableSeats, s.TotalSeats))
            .ToListAsync(cancellationToken);

        var recentRequests = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .Where(b => b.BookingStatus == BookingStatus.Requested || b.BookingStatus == BookingStatus.Pending)
            .OrderByDescending(b => b.RequestedAt)
            .Take(RecentItemsLimit)
            .ToListAsync(cancellationToken);

        var recentlyConfirmed = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .Where(b => b.BookingStatus == BookingStatus.Confirmed)
            .OrderByDescending(b => b.ConfirmedAt)
            .Take(RecentItemsLimit)
            .ToListAsync(cancellationToken);

        return new DashboardDto(
            counters,
            upcomingTrips,
            recentRequests.Select(MapToListItem).ToList(),
            recentlyConfirmed.Select(MapToListItem).ToList());
    }

    private static AdminBookingListItemDto MapToListItem(Domain.Entities.Booking booking) => new(
        booking.BookingId,
        booking.CustomerId,
        booking.Customer.Name,
        booking.Customer.PhoneNumber,
        booking.TripId,
        booking.Trip.Title,
        booking.TripDateSlotId,
        booking.TripDateSlot.StartDate,
        booking.TripDateSlot.EndDate,
        booking.NumberOfSeats,
        booking.TotalAmount,
        booking.AdvanceAmount,
        booking.BookingStatus,
        booking.PaymentStatus,
        booking.BookingSource,
        booking.RequestedAt);
}
