using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

/// <summary>
/// Keeps Admin → CRM in step with real bookings (website, phone or admin): a confirmed booking marks
/// the customer's trip interest Booked, a finished trip Completed, a cancellation Cancelled — creating
/// the interest when the customer booked without one (e.g. straight from the website), or linking the
/// open enquiry for that trip. A second booked trip makes the customer a Repeat customer.
/// Runs after the booking is committed; the caller makes sure a CRM problem never affects the booking.
/// </summary>
public static class CrmBookingSync
{
    private static readonly List<BookingStatus> Booked = [BookingStatus.Confirmed, BookingStatus.Completed];

    public static async Task SyncAsync(IGhumoOdishaDbContext db, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        if (booking is null)
        {
            return;
        }

        CrmStatus target;
        string reason;
        switch (booking.BookingStatus)
        {
            case BookingStatus.Confirmed:
                target = CrmStatus.Booked;
                reason = $"Booking {booking.Reference} confirmed";
                break;
            case BookingStatus.Completed:
                target = CrmStatus.Completed;
                reason = $"Trip completed ({booking.Reference})";
                break;
            case BookingStatus.Cancelled when booking.ConfirmedAt is not null:
                target = CrmStatus.Cancelled;
                reason = $"Booking {booking.Reference} cancelled";
                break;
            default:
                // Unpaid checkouts, expired or rejected requests never became bookings.
                return;
        }

        var customer = await db.Customers.FirstAsync(c => c.CustomerId == booking.CustomerId, cancellationToken);
        var now = DateTime.UtcNow;

        var interest = await db.CustomerTripInterests.FirstOrDefaultAsync(i => i.BookingId == booking.BookingId, cancellationToken)
            ?? await OpenEnquiryForAsync(db, booking, cancellationToken);
        if (interest is null)
        {
            // Booked without an enquiry in the CRM (e.g. straight from the website): add one.
            interest = new CustomerTripInterest
            {
                CustomerId = customer.CustomerId,
                TripId = booking.TripId,
                Status = CrmStatus.NewLead,
                InterestedAt = booking.RequestedAt,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CustomerTripInterests.Add(interest);
        }

        interest.BookingId = booking.BookingId;
        interest.TripDateSlotId = booking.TripDateSlotId ?? interest.TripDateSlotId;
        interest.Travellers = booking.NumberOfSeats;
        if (target == CrmStatus.Cancelled && !string.IsNullOrWhiteSpace(booking.CancellationReason))
        {
            interest.LostReason = CrmHistory.Trim(booking.CancellationReason, 500);
        }

        CrmHistory.SetInterestStatus(db, customer, interest, target, reason, adminUserId: null, now);

        // Two or more trips booked (and not cancelled) → a repeat customer, whatever happened last.
        var bookedTrips = await db.Bookings.CountAsync(b => b.CustomerId == customer.CustomerId && Booked.Contains(b.BookingStatus), cancellationToken);
        if (bookedTrips >= 2)
        {
            CrmHistory.SetCustomerStatus(db, customer, CrmStatus.RepeatCustomer, $"{bookedTrips} trips booked", adminUserId: null, now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Links every paid booking (confirmed, completed, or cancelled after paying) that the CRM doesn't
    /// know about yet — run once at start-up for bookings made before the CRM existed. Safe to repeat.
    /// </summary>
    public static async Task<int> BackfillAsync(IGhumoOdishaDbContext db, CancellationToken cancellationToken = default)
    {
        var ids = await db.Bookings.AsNoTracking()
            .Where(b => (b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed
                         || (b.BookingStatus == BookingStatus.Cancelled && b.ConfirmedAt != null))
                     && !db.CustomerTripInterests.Any(i => i.BookingId == b.BookingId))
            .OrderBy(b => b.ConfirmedAt)
            .Select(b => b.BookingId)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await SyncAsync(db, id, cancellationToken);
            // One booking at a time: forget tracked rows so a long backfill stays light.
            db.ChangeTracker.Clear();
        }

        return ids.Count;
    }

    /// <summary>The customer's open enquiry for this trip: same departure first, then one with no date yet.</summary>
    private static Task<CustomerTripInterest?> OpenEnquiryForAsync(IGhumoOdishaDbContext db, Booking booking, CancellationToken cancellationToken) =>
        db.CustomerTripInterests
            .Where(i => i.CustomerId == booking.CustomerId && i.TripId == booking.TripId && i.BookingId == null
                     && i.Status != CrmStatus.Cancelled && i.Status != CrmStatus.NotInterested && i.Status != CrmStatus.Completed
                     && (i.TripDateSlotId == null || i.TripDateSlotId == booking.TripDateSlotId))
            .OrderByDescending(i => i.TripDateSlotId == booking.TripDateSlotId)
            .ThenByDescending(i => i.InterestedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
