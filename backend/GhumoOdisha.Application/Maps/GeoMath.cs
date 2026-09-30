namespace GhumoOdisha.Application.Maps;

public static class GeoMath
{
    /// <summary>
    /// Ray-casting point-in-polygon on raw lat/lng. Fine at city scale (zones are a few km across), where
    /// the earth's curvature is irrelevant. The polygon is implicitly closed.
    /// </summary>
    public static bool IsInside(GeoPoint point, IReadOnlyList<GeoPoint> polygon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Latitude > point.Latitude) != (b.Latitude > point.Latitude)
                && point.Longitude < (b.Longitude - a.Longitude) * (point.Latitude - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Metres → whole km, always rounded up (1.2 km is charged as 2 km).</summary>
    public static int MetresToKm(int metres) => metres <= 0 ? 0 : (int)Math.Ceiling(metres / 1000.0);
}
