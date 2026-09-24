namespace GhumoOdisha.Application.Destinations.Dtos;

public record DestinationSummaryDto(
    int DestinationId,
    string Name,
    string Slug,
    string? Tagline,
    string? HeroImageUrl,
    int TripCount,
    decimal? StartingPrice);

public record DestinationDetailDto(
    int DestinationId,
    string Name,
    string Slug,
    string? Tagline,
    string? Region,
    string? HeroImageUrl,
    string? AboutText,
    string? BestSeason,
    string? DistanceFromBhubaneswar,
    string? IdealDuration,
    string? KnownFor,
    int TripCount);

public record AdminDestinationListItemDto(
    int DestinationId,
    string Name,
    string Slug,
    bool IsActive,
    int DisplayOrder,
    string? HeroImageUrl,
    int TripCount);

public record AdminDestinationDetailDto(
    int DestinationId,
    string Name,
    string Slug,
    string? Tagline,
    string? Region,
    string? HeroImageUrl,
    string? AboutText,
    string? BestSeason,
    string? DistanceFromBhubaneswar,
    string? IdealDuration,
    string? KnownFor,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<int> TripIds,
    IReadOnlyList<string> TripTitles);

public record CreateDestinationRequest(
    string Name,
    string Slug,
    string? Tagline,
    string? Region,
    string? AboutText,
    string? BestSeason,
    string? DistanceFromBhubaneswar,
    string? IdealDuration,
    string? KnownFor,
    bool IsActive,
    int DisplayOrder);

public record UpdateDestinationRequest(
    string Name,
    string Slug,
    string? Tagline,
    string? Region,
    string? AboutText,
    string? BestSeason,
    string? DistanceFromBhubaneswar,
    string? IdealDuration,
    string? KnownFor,
    bool IsActive,
    int DisplayOrder);
