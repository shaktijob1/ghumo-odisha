using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

public class CarPhoto
{
    public int CarPhotoId { get; set; }
    public int CarId { get; set; }
    public CarPhotoKind Kind { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Car Car { get; set; } = null!;
}
