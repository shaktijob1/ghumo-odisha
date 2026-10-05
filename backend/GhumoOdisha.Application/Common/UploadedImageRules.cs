using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Common;

/// <summary>The same JPG / PNG / WebP, 5 MB limit every site photo upload uses.</summary>
public static class UploadedImageRules
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    public static void Validate(UploadedImage image)
    {
        var errors = new List<string>();

        if (!AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only JPG, PNG or WebP images are allowed.");
        }

        if (image.Length <= 0 || image.Length > MaxSizeBytes)
        {
            errors.Add("Image must be no larger than 5 MB.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }

    /// <summary>Trims a caption to null when blank; rejects one longer than <paramref name="max"/>.</summary>
    public static string? CleanCaption(string? caption, int max = 150)
    {
        var clean = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (clean is not null && clean.Length > max)
        {
            throw new ValidationAppException([$"Caption must be {max} characters or fewer."]);
        }
        return clean;
    }
}
