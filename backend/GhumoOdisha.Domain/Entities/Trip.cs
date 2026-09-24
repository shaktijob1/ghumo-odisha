using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

public class Trip
{
    public int TripId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal AmountPerPerson { get; set; }
    public bool IncludesBreakfast { get; set; }
    public bool IncludesLunch { get; set; }
    public bool IncludesDinner { get; set; }
    public bool IncludesStay { get; set; }
    public bool IncludesCoordinator { get; set; }
    public TripStatus Status { get; set; }
    public string? ItineraryPdfUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<TripPhoto> TripPhotos { get; set; } = new List<TripPhoto>();
    public ICollection<TripHighlight> TripHighlights { get; set; } = new List<TripHighlight>();
    public ICollection<RoomPhoto> RoomPhotos { get; set; } = new List<RoomPhoto>();
    public ICollection<VehiclePhoto> VehiclePhotos { get; set; } = new List<VehiclePhoto>();
    public ICollection<PickupPoint> PickupPoints { get; set; } = new List<PickupPoint>();
    public ICollection<ItineraryDay> ItineraryDays { get; set; } = new List<ItineraryDay>();
    public ICollection<TripDateSlot> TripDateSlots { get; set; } = new List<TripDateSlot>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Destination> Destinations { get; set; } = new List<Destination>();
}
