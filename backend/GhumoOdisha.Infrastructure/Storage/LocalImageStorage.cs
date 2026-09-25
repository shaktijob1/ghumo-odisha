using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Storage;

/// <summary>
/// Saves uploads as-is under <see cref="ImageStorageOptions.BasePath"/>, with a random file name.
/// The file's first bytes decide what it is — never the name or content type the browser sent —
/// and the saved extension comes from that, so a renamed script can't be stored as a ".jpg".
/// </summary>
public class LocalImageStorage(IOptions<ImageStorageOptions> options) : IImageStorage
{
    private readonly ImageStorageOptions _options = options.Value;

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, string subFolder, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var isPdfUpload = string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        var extension = DetectExtension(bytes);
        if (extension is null || (extension == ".pdf") != isPdfUpload)
        {
            throw new ValidationAppException([isPdfUpload
                ? "That file isn't a valid PDF."
                : "That file isn't a valid image. Please upload a JPG, PNG or WebP photo."]);
        }

        var folderPath = Path.Combine(_options.BasePath, subFolder);
        Directory.CreateDirectory(folderPath);

        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(folderPath, uniqueName), bytes, cancellationToken);

        return $"{_options.PublicUrlPrefix}/{subFolder}/{uniqueName}";
    }

    public void Delete(string relativeUrl)
    {
        var prefix = $"{_options.PublicUrlPrefix}/";
        if (!relativeUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = relativeUrl[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_options.BasePath, relativePath);
        DeleteIfExists(filePath);

        // Photos uploaded while automatic WebP processing was enabled also have a phone-sized copy.
        if (filePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            DeleteIfExists(Path.ChangeExtension(filePath, null) + "-sm.webp");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Identifies JPG, PNG, WebP and PDF by their signature bytes; null for anything else.</summary>
    private static string? DetectExtension(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return ".jpg";
        }

        if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        // "%PDF-" may be preceded by a few junk bytes in some real-world files; look near the start.
        if (head[..Math.Min(head.Length, 1024)].IndexOf("%PDF-"u8) >= 0)
        {
            return ".pdf";
        }

        return null;
    }
}
