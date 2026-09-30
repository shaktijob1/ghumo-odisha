namespace GhumoOdisha.Application.Maps;

/// <summary>
/// Google Maps keys ("GoogleMaps" config section). Keep both out of source control — user-secrets in
/// development, GoogleMaps__BrowserApiKey / GoogleMaps__ServerApiKey environment variables in production.
/// </summary>
public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Public key the Angular app loads the Maps JavaScript + Places libraries with. Restrict it in Google
    /// Cloud Console to the site's HTTP referrers. Served to the browser through /api/features.
    /// </summary>
    public string BrowserApiKey { get; set; } = "";

    /// <summary>
    /// Key the API uses for Routes (driving distance) and Geocoding (pickup PIN code). Never sent to the
    /// browser; restrict it to the server's IP. Falls back to <see cref="BrowserApiKey"/> when empty.
    /// </summary>
    public string ServerApiKey { get; set; } = "";

    /// <summary>
    /// Map ID for the admin / booking maps (required by Google's Advanced Markers). "DEMO_MAP_ID" works for
    /// testing; create a real one under Google Cloud → Map Management for production.
    /// </summary>
    public string MapId { get; set; } = "DEMO_MAP_ID";

    public string EffectiveServerKey => string.IsNullOrWhiteSpace(ServerApiKey) ? BrowserApiKey : ServerApiKey;
}
