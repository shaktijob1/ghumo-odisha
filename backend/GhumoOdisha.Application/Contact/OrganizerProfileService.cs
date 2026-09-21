using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Contact;

/// <summary>Manages the single organizer photo shown on the customer dashboard's contact section.</summary>
public class OrganizerProfileService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : IOrganizerProfileService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public async Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default)
    {
        var photo = await db.OrganizerPhotos.FirstOrDefaultAsync(cancellationToken);
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

        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "organizer", cancellationToken);

        var existing = await db.OrganizerPhotos.FirstOrDefaultAsync(cancellationToken);
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
            db.OrganizerPhotos.Add(new OrganizerPhoto { ImageUrl = url, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
