using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;

namespace GhumoOdisha.Application.Bookings;

public interface IBookingService
{
    // Customer
    Task<CreateBookingResult> RequestBookingAsync(int customerId, CreateBookingRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<BookingResponseDto>> GetCustomerBookingsAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<BookingResponseDto> GetCustomerBookingDetailAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Self-service cancel: only allowed up to 72 hours before the trip starts, and
    /// triggers a real Razorpay refund when the booking was paid online.</summary>
    Task<BookingResponseDto> CancelOwnBookingAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResult<AdminBookingListItemDto>> GetAdminBookingsAsync(AdminBookingFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminBookingDetailDto> GetAdminBookingDetailAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminBookingListItemDto>> GetBookingsForTripAsync(int tripId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminBookingListItemDto>> GetBookingsForDateSlotAsync(int dateSlotId, CancellationToken cancellationToken = default);

    Task<int> CreateManualBookingAsync(CreateManualBookingRequest request, CancellationToken cancellationToken = default);

    Task ConfirmBookingAsync(int bookingId, ConfirmBookingRequest request, CancellationToken cancellationToken = default);

    Task RejectBookingAsync(int bookingId, RejectBookingRequest request, CancellationToken cancellationToken = default);

    Task CancelBookingAsync(int bookingId, CancelBookingRequest request, CancellationToken cancellationToken = default);

    Task<int> PromoteCompletedBookingsAsync(CancellationToken cancellationToken = default);
}
