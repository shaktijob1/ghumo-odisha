using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Contact;

/// <summary>
/// The single Travel Organizer card on the home page: photo plus texts the admin can change. Texts
/// left empty fall back to appsettings OrganizerContact (name, role, phone, email, Instagram) or the
/// built-in wording below, so the card is never blank.
/// </summary>
public class OrganizerProfileService(IGhumoOdishaDbContext db, IImageStorage imageStorage, IOptions<OrganizerContactOptions> options) : IOrganizerProfileService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public const string DefaultQuote = "Ask anything about trips, itineraries or custom plans. I'm here to help!";
    public const string DefaultPhotoCaption = "Let's plan your next adventure";
    public const string DefaultHelpTitle = "Need help planning your trip?";
    public const string DefaultHelpText = "Ask about trips, itineraries or custom plans.";

    public async Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default)
    {
        var photo = await db.OrganizerPhotos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return photo?.ImageUrl;
    }

    public async Task<OrganizerCard> GetCardAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.OrganizerPhotos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var d = Defaults();
        return new OrganizerCard(
            row?.Name ?? d.Name,
            row?.Role ?? d.Role,
            row?.Phone ?? d.Phone,
            row?.Email ?? d.Email,
            row?.InstagramUrl ?? d.InstagramUrl,
            row?.Quote ?? d.Quote,
            row?.PhotoCaption ?? d.PhotoCaption,
            row?.HelpTitle ?? d.HelpTitle,
            row?.HelpText ?? d.HelpText,
            row?.ImageUrl);
    }

    public async Task<OrganizerProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.OrganizerPhotos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new OrganizerProfileDto(row?.ImageUrl, row?.Name, row?.Role, row?.Phone, row?.Email, row?.InstagramUrl,
            row?.Quote, row?.PhotoCaption, row?.HelpTitle, row?.HelpText, Defaults());
    }

    public async Task UpdateProfileAsync(UpdateOrganizerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var row = await LoadOrCreateRowAsync(cancellationToken);
        row.Name = Clean(request.Name);
        row.Role = Clean(request.Role);
        row.Phone = Clean(request.Phone);
        row.Email = Clean(request.Email);
        row.InstagramUrl = Clean(request.InstagramUrl);
        row.Quote = Clean(request.Quote);
        row.PhotoCaption = Clean(request.PhotoCaption);
        row.HelpTitle = Clean(request.HelpTitle);
        row.HelpText = Clean(request.HelpText);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPhotoAsync(UploadedImage image, CancellationToken cancellationToken = default)
    {
        if (!AllowedImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(["Only JPG, PNG or WebP images are allowed."]);
        }

        if (image.Length <= 0 || image.Length > MaxImageSizeBytes)
        {
            throw new ValidationAppException(["Image must be no larger than 5 MB."]);
        }

        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "organizer", cancellationToken);

        var row = await LoadOrCreateRowAsync(cancellationToken);
        var oldUrl = row.ImageUrl;
        row.ImageUrl = url;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(oldUrl))
        {
            imageStorage.Delete(oldUrl);
        }
    }

    private OrganizerProfileDefaults Defaults()
    {
        var o = options.Value;
        return new OrganizerProfileDefaults(
            string.IsNullOrWhiteSpace(o.Name) ? "Ghumo Odisha Organizer" : o.Name,
            string.IsNullOrWhiteSpace(o.Role) ? "Your Travel Expert" : o.Role,
            o.Phone,
            o.Email,
            string.IsNullOrWhiteSpace(o.InstagramUrl) ? null : o.InstagramUrl,
            DefaultQuote, DefaultPhotoCaption, DefaultHelpTitle, DefaultHelpText,
            o.WhatsAppNumber);
    }

    private async Task<OrganizerPhoto> LoadOrCreateRowAsync(CancellationToken cancellationToken)
    {
        var row = await db.OrganizerPhotos.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new OrganizerPhoto { UpdatedAt = DateTime.UtcNow };
            db.OrganizerPhotos.Add(row);
        }

        return row;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
