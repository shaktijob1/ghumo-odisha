using GhumoOdisha.Application.Seo;

namespace GhumoOdisha.Tests;

/// <summary>Readable trip URLs (/trips/1-puri-konark) — old and new links must both resolve.</summary>
public class SeoSlugTests
{
    [Theory]
    [InlineData(1, "Puri • Konark • Satapada", "/trips/1-puri-konark-satapada")]
    [InlineData(7, "Koraput Escape – 3D/2N!", "/trips/7-koraput-escape-3d-2n")]
    [InlineData(9, "•••", "/trips/9")]
    public void TripPath_BuildsReadableSlug(int id, string title, string expected) =>
        Assert.Equal(expected, SeoSlug.TripPath(id, title));

    [Theory]
    [InlineData("1-puri-konark-satapada", 1)]
    [InlineData("1", 1)]
    [InlineData("42-renamed-trip", 42)]
    public void ParseTripId_ReadsLeadingNumber(string segment, int expected) =>
        Assert.Equal(expected, SeoSlug.ParseTripId(segment));

    [Theory]
    [InlineData("puri-konark")]
    [InlineData("12abc")]
    [InlineData("")]
    public void ParseTripId_RejectsSegmentsWithoutAnId(string segment) =>
        Assert.Null(SeoSlug.ParseTripId(segment));
}
