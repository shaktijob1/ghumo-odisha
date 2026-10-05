using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Homepage;

public record TravelMomentDto(int TravelMomentId, string ImageUrl, string? Caption, int DisplayOrder);

/// <summary>The home page's "Real Travel Moments" photo gallery.</summary>
public interface ITravelMomentService
{
    /// <summary>Most photos the gallery holds; the admin is asked for 5 to 10.</summary>
    const int MaxPhotos = 10;

    Task<IReadOnlyList<TravelMomentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TravelMomentDto> AddAsync(UploadedImage image, string? caption, CancellationToken cancellationToken = default);

    Task UpdateCaptionAsync(int travelMomentId, string? caption, CancellationToken cancellationToken = default);

    /// <summary>Moves a photo one place earlier (-1) or later (+1) in the gallery.</summary>
    Task MoveAsync(int travelMomentId, int direction, CancellationToken cancellationToken = default);

    Task DeleteAsync(int travelMomentId, CancellationToken cancellationToken = default);
}
