namespace GhumoOdisha.Application.Holidays;

/// <summary>A holiday destination card in the home page's Holidays tab.</summary>
public record HolidayDestinationDto(int HolidayDestinationId, string Name, string? Tagline, string? CoverImageUrl, int PackageCount, decimal? StartingPrice);

/// <summary>A holiday package card. No online booking: the card's button opens WhatsApp.</summary>
public record HolidayPackageDto(
    int HolidayPackageId,
    string Title,
    string? Summary,
    int Days,
    int Nights,
    string DurationLabel,
    decimal? PriceFrom,
    IReadOnlyList<string> Highlights,
    string? CoverImageUrl,
    int? HolidayDestinationId,
    string? DestinationName);

public record AdminHolidayDestinationDto(int HolidayDestinationId, string Name, string? Tagline, string? CoverImageUrl, int DisplayOrder, bool IsActive, int PackageCount);

public record AdminHolidayPackageDto(
    int HolidayPackageId,
    string Title,
    string? Summary,
    int Days,
    int Nights,
    string DurationLabel,
    decimal? PriceFrom,
    IReadOnlyList<string> Highlights,
    string? CoverImageUrl,
    int? HolidayDestinationId,
    string? DestinationName,
    int DisplayOrder,
    bool IsActive);

public record SaveHolidayDestinationRequest(string Name, string? Tagline, int DisplayOrder, bool IsActive);

public record SaveHolidayPackageRequest(
    string Title,
    string? Summary,
    int Days,
    int Nights,
    decimal? PriceFrom,
    IReadOnlyList<string>? Highlights,
    int? HolidayDestinationId,
    int DisplayOrder,
    bool IsActive);
