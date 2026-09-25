using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings;

/// <summary>
/// Appends a step to a booking's history. Always called in the same unit of work (and so the same
/// transaction) as the change it describes, so the timeline can never claim something that rolled back.
/// </summary>
public static class BookingTimeline
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string System = "System";

    public static void Add(IGhumoOdishaDbContext db, Booking booking, BookingEventType type, string title,
        string? description, string actor, bool visibleToCustomer = true, DateTime? at = null)
    {
        var evt = new BookingEvent
        {
            EventType = type,
            Title = Truncate(title, 120)!,
            Description = Truncate(description, 600),
            Actor = actor,
            IsVisibleToCustomer = visibleToCustomer,
            CreatedAt = at ?? DateTime.UtcNow
        };

        // A booking that hasn't been saved yet has no id — attach through the navigation instead.
        if (booking.BookingId == 0)
        {
            evt.Booking = booking;
        }
        else
        {
            evt.BookingId = booking.BookingId;
        }

        db.BookingEvents.Add(evt);
    }

    public static string Money(decimal amount) => $"₹{amount:N0}";

    public static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Razorpay => "online (Razorpay)",
        PaymentMethod.Cash => "cash",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.BankTransfer => "bank transfer",
        _ => "other"
    };

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..(max - 1)] + "…";
}
