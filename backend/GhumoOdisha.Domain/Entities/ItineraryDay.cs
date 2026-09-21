namespace GhumoOdisha.Domain.Entities;

public class ItineraryDay
{
    public int ItineraryDayId { get; set; }
    public int TripId { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int DisplayOrder { get; set; }

    public Trip Trip { get; set; } = null!;
    public ICollection<ItineraryPoint> ItineraryPoints { get; set; } = new List<ItineraryPoint>();
}
