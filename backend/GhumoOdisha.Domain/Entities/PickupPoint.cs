namespace GhumoOdisha.Domain.Entities;

public class PickupPoint
{
    public int PickupPointId { get; set; }
    public int TripId { get; set; }
    public string Location { get; set; } = null!;
    public string Time { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
}
