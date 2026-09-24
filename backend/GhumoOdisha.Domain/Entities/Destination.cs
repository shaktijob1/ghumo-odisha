namespace GhumoOdisha.Domain.Entities;

public class Destination
{
    public int DestinationId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Tagline { get; set; }
    public string? Region { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? AboutText { get; set; }
    public string? BestSeason { get; set; }
    public string? DistanceFromBhubaneswar { get; set; }
    public string? IdealDuration { get; set; }
    public string? KnownFor { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
