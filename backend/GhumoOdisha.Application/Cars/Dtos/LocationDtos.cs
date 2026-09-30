namespace GhumoOdisha.Application.Cars.Dtos;

public record GeoPointDto(double Latitude, double Longitude);

/// <summary>A place picked on the map (autocomplete suggestion or "use my current location").</summary>
public record TripPlaceRequest(double Latitude, double Longitude, string Label);

// ---------- Service areas (admin) ----------

public record ServiceAreaDto(
    int ServiceAreaId,
    string Name,
    bool IsActive,
    /// <summary>Drawn zone corners, empty when the area is PIN codes only.</summary>
    IReadOnlyList<GeoPointDto> Boundary,
    IReadOnlyList<string> Pincodes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveServiceAreaRequest(string Name, bool IsActive, IReadOnlyList<GeoPointDto>? Boundary, IReadOnlyList<string>? Pincodes);

// ---------- Pickup check (public) ----------

public record PublicServiceZoneDto(string Name, IReadOnlyList<GeoPointDto> Boundary);

public record PickupCheckRequest(double Latitude, double Longitude);

public record PickupCheckDto(bool IsServiceable, string? AreaName, string? Message);

// ---------- Driver starting point ----------

public record SetBaseLocationRequest(double Latitude, double Longitude, string Label);
