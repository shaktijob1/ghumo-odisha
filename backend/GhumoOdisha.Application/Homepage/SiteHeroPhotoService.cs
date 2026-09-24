using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Homepage;

/// <summary>Manages the single dashboard hero banner photo, uploaded independently of any trip.</summary>
public class SiteHeroPhotoService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : ISiteHeroPhotoService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public async Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default)
    {
        var photo = await db.SiteHeroPhotos.FirstOrDefaultAsync(cancellationToken);
        return photo?.ImageUrl;
    }

    public async Task SetPhotoAsync(UploadedImage image, CancellationToken cancellationToken = default)
    {
        if (!AllowedImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(["Only JPG or PNG images are allowed."]);
        }

        if (image.Length <= 0 || image.Length > MaxImageSizeBytes)
        {
            throw new ValidationAppException(["Image must be no larger than 5 MB."]);
        }

        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "hero", cancellationToken);

        var existing = await db.SiteHeroPhotos.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            var oldUrl = existing.ImageUrl;
            existing.ImageUrl = url;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            imageStorage.Delete(oldUrl);
        }
        else
        {
            db.SiteHeroPhotos.Add(new SiteHeroPhoto { ImageUrl = url, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
