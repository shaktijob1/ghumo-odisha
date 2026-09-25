using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Destinations;

public interface IDestinationService
{
    // Public
    Task<IReadOnlyList<DestinationSummaryDto>> GetActiveDestinationsAsync(CancellationToken cancellationToken = default);

    Task<DestinationDetailDto> GetDestinationBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PagedResult<TripSummaryDto>> GetDestinationTripsAsync(string slug, int page, int pageSize, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResult<AdminDestinationListItemDto>> GetAdminDestinationsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    Task<AdminDestinationDetailDto> GetAdminDestinationDetailAsync(int destinationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminDestinationListItemDto>> GetAllForPickerAsync(CancellationToken cancellationToken = default);

    Task<int> CreateDestinationAsync(CreateDestinationRequest request, CancellationToken cancellationToken = default);

    Task UpdateDestinationAsync(int destinationId, UpdateDestinationRequest request, CancellationToken cancellationToken = default);

    Task DeleteDestinationAsync(int destinationId, CancellationToken cancellationToken = default);

    Task UpdateDestinationHeroImageAsync(int destinationId, UploadedImage image, CancellationToken cancellationToken = default);

    /// <summary>Photo for destination cards (trending row). Cards fall back to the hero photo when unset.</summary>
    Task UpdateDestinationCoverImageAsync(int destinationId, UploadedImage image, CancellationToken cancellationToken = default);
}
