using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Homepage;

public interface ISiteHeroPhotoService
{
    Task<string?> GetPhotoUrlAsync(CancellationToken cancellationToken = default);

    Task SetPhotoAsync(UploadedImage image, CancellationToken cancellationToken = default);
}
