using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Collections;

/// <summary>
/// Admin "Collections" desk: per trip (and departure), every confirmed booking with what's been paid and
/// what's still due, so a coordinator can collect balances on the trip. Recording a collected amount goes
/// through <see cref="Bookings.IBookingService.AddPaymentAsync"/> — the same ledger as the booking page.
/// </summary>
public interface ICollectionService
{
    Task<IReadOnlyList<CollectionTripDto>> GetTripsAsync(CancellationToken cancellationToken = default);

    Task<CollectionSheetDto> GetSheetAsync(int tripId, int? tripDateSlotId, CancellationToken cancellationToken = default);

    Task<PaymentQrDto?> GetPaymentQrAsync(CancellationToken cancellationToken = default);

    Task<PaymentQrDto> SetPaymentQrAsync(UploadedImage image, string? caption, CancellationToken cancellationToken = default);

    Task RemovePaymentQrAsync(CancellationToken cancellationToken = default);
}
