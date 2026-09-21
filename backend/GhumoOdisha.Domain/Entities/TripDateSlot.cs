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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
