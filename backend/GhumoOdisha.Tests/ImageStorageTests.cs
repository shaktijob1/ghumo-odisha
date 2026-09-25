using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Uploads are stored as-is, but only when their first bytes prove they are what they claim.</summary>
public sealed class ImageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "go-img-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalImageStorage _storage;

    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] WebpBytes = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    public ImageStorageTests()
    {
        _storage = new LocalImageStorage(Options.Create(new ImageStorageOptions { BasePath = _root, PublicUrlPrefix = "/uploads" }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string DiskPath(string url) => Path.Combine(_root, url["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar));

    [Theory]
    [InlineData("jpeg", ".jpg")]
    [InlineData("png", ".png")]
    [InlineData("webp", ".webp")]
    public async Task RealImage_IsSavedUnchanged_WithTheExtensionOfWhatItActuallyIs(string kind, string expectedExtension)
    {
        var bytes = kind switch { "jpeg" => JpegBytes, "png" => PngBytes, _ => WebpBytes };

        // Deliberately misleading name — the stored extension follows the content, not the name.
        var url = await _storage.SaveAsync(new MemoryStream(bytes), "photo.gif", "image/jpeg", "trips");

        Assert.Matches($@"^/uploads/trips/[0-9a-f]{{32}}\{expectedExtension}$", url);
        Assert.Equal(bytes, File.ReadAllBytes(DiskPath(url)));

        _storage.Delete(url);
        Assert.False(File.Exists(DiskPath(url)));
    }

    [Fact]
    public async Task FileThatIsNotReallyAnImage_IsRejected_WhateverItsNameSays()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            _storage.SaveAsync(new MemoryStream("<script>alert(1)</script>"u8.ToArray()), "photo.jpg", "image/jpeg", "trips"));
        Assert.False(Directory.Exists(Path.Combine(_root, "trips")) && Directory.EnumerateFiles(Path.Combine(_root, "trips")).Any());
    }

    [Fact]
    public async Task PdfPosingAsAnImage_IsRejected()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            _storage.SaveAsync(new MemoryStream("%PDF-1.4\n%%EOF"u8.ToArray()), "photo.jpg", "image/jpeg", "trips"));
    }

    [Fact]
    public async Task Pdf_IsCheckedAndStoredAsIs()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            _storage.SaveAsync(new MemoryStream("not a pdf"u8.ToArray()), "itinerary.pdf", "application/pdf", "itineraries"));
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            _storage.SaveAsync(new MemoryStream(JpegBytes), "itinerary.pdf", "application/pdf", "itineraries"));

        var pdfBytes = "%PDF-1.4\n%fake minimal body\n%%EOF"u8.ToArray();
        var url = await _storage.SaveAsync(new MemoryStream(pdfBytes), "itinerary.pdf", "application/pdf", "itineraries");

        Assert.EndsWith(".pdf", url);
        Assert.Equal(pdfBytes, File.ReadAllBytes(DiskPath(url)));
    }
}
