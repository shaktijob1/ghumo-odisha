using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Infrastructure.Persistence.Seed;

/// <summary>
/// Starter WhatsApp templates for Admin → CRM, inserted once by the CrmStarterTemplates migration.
/// Everything trip-specific is a placeholder, so they work for any trip, date or price; admins can
/// edit, duplicate, switch off or delete them.
/// </summary>
public static class CrmTemplateSeed
{
    public static readonly DateTime CreatedAt = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    public static readonly (string Name, CrmTemplateCategory Category, string Body)[] Templates =
    [
        ("New enquiry – trip details", CrmTemplateCategory.NewEnquiry,
            """
            Hi {name} 👋

            Thank you for your interest in our {trip} trip!

            📅 {date} to {return_date}
            💰 ₹{price}/person
            🚐 {vehicle}
            🏨 {stay}

            You can reserve your seat for just ₹{booking_amount}. Shall I book it for you?

            – Ghumo Odisha
            """),
        ("Follow-up – interested customer", CrmTemplateCategory.FollowUp,
            """
            Hi {name} 👋

            You had enquired about our {trip}.

            📅 {date}
            💰 ₹{price}/person
            🚐 {vehicle}
            🏨 {stay}

            We currently have limited seats available.

            Would you like to reserve your seat?

            – Ghumo Odisha
            """),
        ("Itinerary", CrmTemplateCategory.Itinerary,
            """
            Hi {name}, here is the plan for {trip} ({date} to {return_date}).

            📍 Pickup: {pickup}
            🚐 {vehicle}

            The full day-wise itinerary is on our website. Let me know if you have any questions!

            – Ghumo Odisha
            """),
        ("Early booking offer", CrmTemplateCategory.EarlyBooking,
            """
            Hi {name} 🎉

            Early booking is open for {trip} on {date}: ₹{price}/person.

            Reserve with just ₹{booking_amount} before the offer ends — only {available_seats} seats left.

            – Ghumo Odisha
            """),
        ("Limited seats", CrmTemplateCategory.LimitedSeats,
            """
            Hi {name}, only {available_seats} of {total_seats} seats are left for {trip} on {date}.

            Reply YES and I'll hold one for you.

            – Ghumo Odisha
            """),
        ("Trip confirmed", CrmTemplateCategory.TripConfirmed,
            """
            Hi {name} 🟢 Good news — {trip} on {date} is confirmed!

            Your booking {booking_id} for {travellers} traveller(s) is all set.
            📍 Pickup: {pickup}

            – Ghumo Odisha
            """),
        ("Reservation reminder", CrmTemplateCategory.PaymentReminder,
            """
            Hi {name}, a gentle reminder to reserve your seat(s) for {trip} on {date}.

            ₹{booking_amount} confirms {travellers} seat(s). Seats are filling up!

            – Ghumo Odisha
            """),
        ("Trip reminder", CrmTemplateCategory.TripReminder,
            """
            Hi {name} 👋 Your {trip} trip starts on {date}.

            📍 Pickup: {pickup}
            Please carry a valid government photo ID. See you soon!

            – Ghumo Odisha
            """),
        ("Balance payment", CrmTemplateCategory.BalancePayment,
            """
            Hi {name}, the balance for booking {booking_id} ({trip}, {date}) is ₹{balance}.

            Please pay it before departure. Thank you!

            – Ghumo Odisha
            """),
        ("Thank you – trip completed", CrmTemplateCategory.TripCompleted,
            """
            Hi {name}, thank you for travelling with us on {trip}! 🙏

            We hope you had a wonderful time.

            – Ghumo Odisha
            """),
        ("Review request", CrmTemplateCategory.ReviewRequest,
            """
            Hi {name}, we'd love to hear about your {trip} trip.

            Could you share a quick review? It helps other travellers a lot. 🙏

            – Ghumo Odisha
            """),
        ("Referral", CrmTemplateCategory.Referral,
            """
            Hi {name}, so glad you enjoyed {trip}! 😊

            Know friends or family who would love a trip like this? Share our number — we'll take great care of them.

            – Ghumo Odisha
            """),
    ];
}
