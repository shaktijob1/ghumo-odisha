using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

public class TripDateSlot
{
    public int TripDateSlotId { get; set; }
    public int TripId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public TripDateSlotStatus Status { get; set; }

    // ---- Departure details managed from Admin → CRM ----
    /// <summary>"AC Traveller", "Innova" — shown in CRM and WhatsApp messages ({vehicle}).</summary>
    public string? Vehicle { get; set; }
    /// <summary>Booked seats needed before the departure is shown as "Trip confirmed". Null = no minimum.</summary>
    public int? MinimumBookedSeats { get; set; }
    /// <summary>Per-person price for this departure; null uses the trip's price.</summary>
    public decimal? PricePerPerson { get; set; }
    /// <summary>Early-booking per-person price, charged up to and including <see cref="EarlyBirdEndsOn"/>.</summary>
    public decimal? EarlyBirdPrice { get; set; }
    public DateOnly? EarlyBirdEndsOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
