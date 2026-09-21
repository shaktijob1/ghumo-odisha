using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Contact;

public interface IOrganizerProfileService
{
    Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default);

    Task SetPhotoAsync(UploadedImage image, CancellationToken cancellationToken = default);
}
