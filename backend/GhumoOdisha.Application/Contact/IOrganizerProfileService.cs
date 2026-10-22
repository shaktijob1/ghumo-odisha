using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Contact;

public interface IOrganizerProfileService
{
    Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default);

    Task SetPhotoAsync(UploadedImage image, CancellationToken cancellationToken = default);

    /// <summary>The Travel Organizer card as the website shows it (stored texts, else the defaults).</summary>
    Task<OrganizerCard> GetCardAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin: stored texts plus the defaults they fall back to.</summary>
    Task<OrganizerProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin: saves the texts; an empty field goes back to its default.</summary>
    Task UpdateProfileAsync(UpdateOrganizerProfileRequest request, CancellationToken cancellationToken = default);
}
