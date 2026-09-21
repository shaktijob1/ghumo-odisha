using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips;

public interface ITripService
{
    // Public
    Task<PagedResult<TripSummaryDto>> GetActiveTripsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<TripDetailDto> GetTripDetailAsync(int tripId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DateSlotDto>> GetActiveDateSlotsAsync(int tripId, CancellationToken cancellationToken = default);

    // Admin - trips
    Task<PagedResult<AdminTripListItemDto>> GetAdminTripsAsync(int page, int pageSize, string? search, TripStatus? status, CancellationToken cancellationToken = default);

    Task<AdminTripDetailDto> GetAdminTripDetailAsync(int tripId, CancellationToken cancellationToken = default);

    Task<int> CreateTripAsync(CreateTripRequest request, CancellationToken cancellationToken = default);

    Task UpdateTripAsync(int tripId, UpdateTripRequest request, CancellationToken cancellationToken = default);

    Task DeleteTripAsync(int tripId, CancellationToken cancellationToken = default);

    // Admin - photos
    Task<int> AddTripPhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default);

    Task DeleteTripPhotoAsync(int tripPhotoId, CancellationToken cancellationToken = default);

    Task UpdateTripPhotoOrderAsync(int tripPhotoId, int displayOrder, CancellationToken cancellationToken = default);

    // Admin - highlights
    Task<int> AddTripHighlightAsync(int tripId, AddTripHighlightRequest request, UploadedImage image, CancellationToken cancellationToken = default);

    Task UpdateTripHighlightAsync(int highlightId, UpdateTripHighlightRequest request, UploadedImage? image, CancellationToken cancellationToken = default);

    Task DeleteTripHighlightAsync(int highlightId, CancellationToken cancellationToken = default);

    // Admin - date slots
    Task<int> AddDateSlotAsync(int tripId, AddDateSlotRequest request, CancellationToken cancellationToken = default);

    Task UpdateDateSlotAsync(int dateSlotId, UpdateDateSlotRequest request, CancellationToken cancellationToken = default);

    Task DeleteDateSlotAsync(int dateSlotId, CancellationToken cancellationToken = default);

    // Admin - itinerary
    Task<int> AddItineraryDayAsync(int tripId, AddItineraryDayRequest request, CancellationToken cancellationToken = default);

    Task UpdateItineraryDayAsync(int dayId, UpdateItineraryDayRequest request, CancellationToken cancellationToken = default);

    Task DeleteItineraryDayAsync(int dayId, CancellationToken cancellationToken = default);

    Task<int> AddItineraryPointAsync(int dayId, AddItineraryPointRequest request, CancellationToken cancellationToken = default);

    Task UpdateItineraryPointAsync(int pointId, UpdateItineraryPointRequest request, CancellationToken cancellationToken = default);

    Task DeleteItineraryPointAsync(int pointId, CancellationToken cancellationToken = default);

    // Admin - room photos
    Task<int> AddRoomPhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default);

    Task DeleteRoomPhotoAsync(int roomPhotoId, CancellationToken cancellationToken = default);

    Task UpdateRoomPhotoOrderAsync(int roomPhotoId, int displayOrder, CancellationToken cancellationToken = default);

    // Admin - vehicle photos
    Task<int> AddVehiclePhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default);

    Task DeleteVehiclePhotoAsync(int vehiclePhotoId, CancellationToken cancellationToken = default);

    Task UpdateVehiclePhotoOrderAsync(int vehiclePhotoId, int displayOrder, CancellationToken cancellationToken = default);

    // Admin - pickup points
    Task<int> AddPickupPointAsync(int tripId, AddPickupPointRequest request, CancellationToken cancellationToken = default);

    Task UpdatePickupPointAsync(int pickupPointId, UpdatePickupPointRequest request, CancellationToken cancellationToken = default);

    Task DeletePickupPointAsync(int pickupPointId, CancellationToken cancellationToken = default);
}
