namespace GhumoOdisha.Domain.Entities;

public class ItineraryPoint
{
    public int ItineraryPointId { get; set; }
    public int ItineraryDayId { get; set; }
    public string Time { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int DisplayOrder { get; set; }

    public ItineraryDay ItineraryDay { get; set; } = null!;
}
