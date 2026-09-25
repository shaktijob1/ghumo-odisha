using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Homepage;

public interface ISiteHeroPhotoService
{
    Task<string?> GetPhotoUrlAsync(SiteHeroPage page, CancellationToken cancellationToken = default);

    Task SetPhotoAsync(SiteHeroPage page, UploadedImage image, CancellationToken cancellationToken = default);
}
