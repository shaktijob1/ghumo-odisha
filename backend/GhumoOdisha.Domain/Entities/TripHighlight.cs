namespace GhumoOdisha.Domain.Entities;

public class TripHighlight
{
    public int TripHighlightId { get; set; }
    public int TripId { get; set; }
    public string PlaceName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string PhotoUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
}
