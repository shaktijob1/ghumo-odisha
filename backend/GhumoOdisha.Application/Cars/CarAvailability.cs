using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Cars;

/// <summary>
/// Which car bookings hold a car's time. A booking holds [PickupAt, EndsAt] plus a turnaround buffer
/// on both sides; a running trip holds the car until it's completed, even past its booked end.
/// The actual guarantee against double booking is <see cref="CarBookingService"/> locking the car row
/// (SELECT … FOR UPDATE) around this check — the check alone could race.
/// </summary>
public static class CarAvailability
{
    /// <summary>Confirmed or running bookings, or an unpaid one still inside its payment hold, that haven't ended.</summary>
    public static IQueryable<CarBooking> Upcoming(this IQueryable<CarBooking> bookings, DateTime nowUtc) =>
        bookings.Where(b => b.Status == CarBookingStatus.InProgress
            || (b.Status == CarBookingStatus.Confirmed && b.EndsAt > nowUtc)
            || (b.Status == CarBookingStatus.PendingPayment && b.HoldExpiresAt > nowUtc));

    /// <summary>Bookings that hold a car at any moment of [startUtc, endUtc] (buffer applied both sides).</summary>
    public static IQueryable<CarBooking> Overlapping(this IQueryable<CarBooking> bookings, DateTime startUtc, DateTime endUtc, int bufferMinutes, DateTime nowUtc)
    {
        var endWithBuffer = endUtc.AddMinutes(bufferMinutes);
        var startLessBuffer = startUtc.AddMinutes(-bufferMinutes);
        return bookings.Upcoming(nowUtc).Where(b =>
            b.PickupAt < endWithBuffer
            && (b.EndsAt > startLessBuffer
                // A running trip that overruns still has the car.
                || (b.Status == CarBookingStatus.InProgress && nowUtc > startLessBuffer)));
    }

    public static Task<bool> HasUpcomingBookingsAsync(IGhumoOdishaDbContext db, int carId, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.CarBookings.Where(b => b.CarId == carId).Upcoming(nowUtc).AnyAsync(cancellationToken);

    /// <summary>Customers can book the car: car, driver and active pricing all approved.</summary>
    public static IQueryable<Car> Listed(this IQueryable<Car> cars) =>
        cars.Where(c => c.Status == CarStatus.Approved && c.Driver.Status == DriverStatus.Approved && c.ActivePricingId != null);
}
