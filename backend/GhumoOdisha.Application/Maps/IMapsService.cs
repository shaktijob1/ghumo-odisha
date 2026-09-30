namespace GhumoOdisha.Application.Maps;

/// <summary>A point on the map (WGS84 degrees).</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    public bool IsValid =>
        double.IsFinite(Latitude) && double.IsFinite(Longitude) && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;
}

/// <summary>
/// Server-side map lookups. Distances used for fares always come from here, never from the browser.
/// </summary>
public interface IMapsService
{
    /// <summary>Driving distance in metres by road. Throws <see cref="Exceptions.MapsUnavailableException"/> when Google can't be reached or finds no route.</summary>
    Task<int> DrivingDistanceMetersAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken = default);

    /// <summary>The 6-digit PIN code at a point, or null when Google doesn't know one.</summary>
    Task<string?> PostalCodeAsync(GeoPoint point, CancellationToken cancellationToken = default);
}
