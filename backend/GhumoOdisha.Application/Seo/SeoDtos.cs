namespace GhumoOdisha.Application.Seo;

public record SeoDeparture(DateOnly StartDate, DateOnly EndDate, int AvailableSeats);

public record TripSeoInfo(
    int TripId,
    string Title,
    string Path,
    string Description,
    decimal AmountPerPerson,
    string? DurationLabel,
    string? ImageUrl,
    IReadOnlyList<string> DestinationNames,
    IReadOnlyList<SeoDeparture> UpcomingDepartures);

public record DestinationTripLink(string Title, string Path, decimal AmountPerPerson);

public record DestinationSeoInfo(
    string Name,
    string Slug,
    string? Tagline,
    string? AboutText,
    string? KnownFor,
    string? ImageUrl,
    IReadOnlyList<DestinationTripLink> Trips);

public record SitemapEntry(string Path, DateTime? LastModifiedUtc);

public record HomeSeoInfo(string? HeroImageUrl, IReadOnlyList<DestinationTripLink> Trips);
