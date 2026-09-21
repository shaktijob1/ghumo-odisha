namespace GhumoOdisha.Domain.Entities;

public class TripPhoto
{
    public int TripPhotoId { get; set; }
    public int TripId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
}
