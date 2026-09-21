using System.Data;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Bookings;

public class BookingService(
    IGhumoOdishaDbContext db,
    IRazorpayService razorpay,
    IFast2SmsWhatsAppService whatsApp,
    IOptions<OrganizerContactOptions> organizerContactOptions,
    ILogger<BookingService> logger) : IBookingService
{
    private const int CancellationWindowHours = 72;
    private readonly OrganizerContactOptions _organizerContact = organizerContactOptions.Value;
    // ---------- Customer ----------

    public async Task<CreateBookingResult> RequestBookingAsync(int customerId, CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId.HasValue)
        {
            var existingResult = await TryLoadExistingBookingResultAsync(customerId, request.ClientRequestId.Value, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult;
            }
        }

        var trip = await db.Trips.Include(t => t.TripPhotos).FirstOrDefaultAsync(t => t.TripId == request.TripId && t.Status == TripStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == request.TripDateSlotId && s.TripId == request.TripId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        if (slot.Status != TripDateSlotStatus.Active)
        {
            throw new ConflictException("This date is no longer available.");
        }

        if (request.NumberOfSeats > slot.AvailableSeats)
        {
            throw new ConflictException($"Only {slot.AvailableSeats} seat(s) are available on this date.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        PickupPoint? pickupPoint = null;
        if (request.PickupPointId.HasValue)
        {
            pickupPoint = await db.PickupPoints.FirstOrDefaultAsync(
                p => p.PickupPointId == request.PickupPointId.Value && p.TripId == trip.TripId, cancellationToken)
                ?? throw new NotFoundException("Pickup point not found for this trip.");
        }

        var now = DateTime.UtcNow;
        var totalAmount = trip.AmountPerPerson * request.NumberOfSeats;

        var booking = new Booking
        {
            CustomerId = customerId,
            TripId = trip.TripId,
            TripDateSlotId = slot.TripDateSlotId,
            PickupPointId = pickupPoint?.PickupPointId,
            NumberOfSeats = request.NumberOfSeats,
            AmountPerPerson = trip.AmountPerPerson,
            TotalAmount = totalAmount,
            AdvanceAmount = 0,
            RemainingAmount = totalAmount,
            BookingStatus = BookingStatus.Requested,
            PaymentStatus = PaymentStatus.Unpaid,
            BookingSource = BookingSource.Website,
            CustomerNotes = request.CustomerNotes,
            ClientRequestId = request.ClientRequestId,
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Bookings.Add(booking);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (request.ClientRequestId.HasValue)
        {
            // Lost a race against an identical retry that committed first — return its result instead of erroring.
            var existingResult = await TryLoadExistingBookingResultAsync(customerId, request.ClientRequestId.Value, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult;
            }

            throw;
        }

        var message = BuildWhatsAppMessage(booking, trip, slot, customer);
        await NotifyBookingRequestedAsync(booking, trip, slot, customer, cancellationToken);
        return new CreateBookingResult(MapToResponse(booking, trip.Title, CoverImageUrl(trip.TripPhotos), slot, pickupPoint), message);
    }

    private async Task<CreateBookingResult?> TryLoadExistingBookingResultAsync(int customerId, Guid clientRequestId, CancellationToken cancellationToken)
    {
        var existing = await db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.TripPhotos)
            .Include(b => b.TripDateSlot)
            .Include(b => b.Customer)
            .Include(b => b.PickupPoint)
            .FirstOrDefaultAsync(b => b.CustomerId == customerId && b.ClientRequestId == clientRequestId, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        var message = BuildWhatsAppMessage(existing, existing.Trip, existing.TripDateSlot, existing.Customer);
        return new CreateBookingResult(MapToResponse(existing, existing.Trip.Title, CoverImageUrl(existing.Trip.TripPhotos), existing.TripDateSlot, existing.PickupPoint), message);
    }

    public async Task<PagedResult<BookingResponseDto>> GetCustomerBookingsAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.Where(b => b.CustomerId == customerId).OrderByDescending(b => b.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var bookings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.Trip).ThenInclude(t => t.TripPhotos)
            .Include(b => b.TripDateSlot)
            .Include(b => b.PickupPoint)
            .ToListAsync(cancellationToken);

        var items = bookings.Select(b => MapToResponse(b, b.Trip.Title, CoverImageUrl(b.Trip.TripPhotos), b.TripDateSlot, b.PickupPoint)).ToList();
        return new PagedResult<BookingResponseDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<BookingResponseDto> GetCustomerBookingDetailAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t.TripPhotos)
            .Include(b => b.TripDateSlot)
            .Include(b => b.PickupPoint)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        return MapToResponse(booking, booking.Trip.Title, CoverImageUrl(booking.Trip.TripPhotos), booking.TripDateSlot, booking.PickupPoint);
    }

    // ---------- Admin: reads ----------

    public async Task<PagedResult<AdminBookingListItemDto>> GetAdminBookingsAsync(AdminBookingFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .AsQueryable();

        if (filter.BookingStatus.HasValue)
        {
            query = query.Where(b => b.BookingStatus == filter.BookingStatus.Value);
        }

        if (filter.PaymentStatus.HasValue)
        {
            query = query.Where(b => b.PaymentStatus == filter.PaymentStatus.Value);
        }

        if (filter.TripId.HasValue)
        {
            query = query.Where(b => b.TripId == filter.TripId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(b => b.TripDateSlot.StartDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(b => b.TripDateSlot.StartDate <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(b =>
                b.Customer.Name.Contains(filter.Search) ||
                b.Customer.PhoneNumber.Contains(filter.Search) ||
                b.Trip.Title.Contains(filter.Search));
        }

        query = query.OrderByDescending(b => b.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var bookings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = bookings.Select(MapToAdminListItem).ToList();
        return new PagedResult<AdminBookingListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<AdminBookingDetailDto> GetAdminBookingDetailAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        var customerBookingCount = await db.Bookings.CountAsync(b => b.CustomerId == booking.CustomerId, cancellationToken);

        return new AdminBookingDetailDto(
            booking.BookingId,
            booking.CustomerId,
            booking.Customer.Name,
            booking.Customer.PhoneNumber,
            booking.Customer.Email,
            customerBookingCount,
            booking.TripId,
            booking.Trip.Title,
            booking.TripDateSlotId,
            booking.TripDateSlot.StartDate,
            booking.TripDateSlot.EndDate,
            booking.TripDateSlot.AvailableSeats,
            booking.NumberOfSeats,
            booking.AmountPerPerson,
            booking.TotalAmount,
            booking.AdvanceAmount,
            booking.RemainingAmount,
            booking.BookingStatus,
            booking.PaymentStatus,
            booking.BookingSource,
            booking.CustomerNotes,
            booking.AdminNotes,
            booking.RequestedAt,
            booking.ConfirmedAt,
            booking.CancelledAt);
    }

    public async Task<IReadOnlyList<AdminBookingListItemDto>> GetBookingsForTripAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var bookings = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .Where(b => b.TripId == tripId)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(MapToAdminListItem).ToList();
    }

    public async Task<IReadOnlyList<AdminBookingListItemDto>> GetBookingsForDateSlotAsync(int dateSlotId, CancellationToken cancellationToken = default)
    {
        var bookings = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .Where(b => b.TripDateSlotId == dateSlotId)
            .OrderByDescending(b => b.RequestedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(MapToAdminListItem).ToList();
    }

    // ---------- Admin: writes ----------

    public async Task<int> CreateManualBookingAsync(CreateManualBookingRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == request.TripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == request.TripDateSlotId && s.TripId == request.TripId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        if (request.NumberOfSeats > slot.TotalSeats)
        {
            throw new ConflictException($"This date slot only has {slot.TotalSeats} total seats.");
        }

        int customerId;
        if (request.CustomerId.HasValue)
        {
            var exists = await db.Customers.AnyAsync(c => c.CustomerId == request.CustomerId.Value, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException("Customer not found.");
            }

            customerId = request.CustomerId.Value;
        }
        else
        {
            var phoneTaken = await db.Customers.AnyAsync(c => c.PhoneNumber == request.NewCustomerPhoneNumber, cancellationToken);
            if (phoneTaken)
            {
                throw new ConflictException("A customer with this phone number already exists. Use the existing-customer picker instead.");
            }

            var now = DateTime.UtcNow;
            var newCustomer = new Customer
            {
                Name = request.NewCustomerName!,
                PhoneNumber = request.NewCustomerPhoneNumber!,
                Email = request.NewCustomerEmail,
                IsVerified = false,
                FailedLoginAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Customers.Add(newCustomer);
            await db.SaveChangesAsync(cancellationToken);
            customerId = newCustomer.CustomerId;
        }

        var totalAmount = trip.AmountPerPerson * request.NumberOfSeats;
        if (request.AdvanceAmount > totalAmount)
        {
            throw new ValidationAppException(["Advance amount cannot exceed the total amount."]);
        }

        var bookingNow = DateTime.UtcNow;
        var booking = new Booking
        {
            CustomerId = customerId,
            TripId = trip.TripId,
            TripDateSlotId = slot.TripDateSlotId,
            NumberOfSeats = request.NumberOfSeats,
            AmountPerPerson = trip.AmountPerPerson,
            TotalAmount = totalAmount,
            AdvanceAmount = request.AdvanceAmount,
            RemainingAmount = totalAmount - request.AdvanceAmount,
            BookingStatus = request.InitialStatus,
            PaymentStatus = ComputePaymentStatus(request.AdvanceAmount, totalAmount),
            BookingSource = request.BookingSource,
            AdminNotes = request.AdminNotes,
            RequestedAt = bookingNow,
            CreatedAt = bookingNow,
            UpdatedAt = bookingNow
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        return booking.BookingId;
    }

    public async Task ConfirmBookingAsync(int bookingId, ConfirmBookingRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending))
            {
                throw new ConflictException("Only requested or pending bookings can be confirmed.");
            }

            var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.TripId == booking.TripId, cancellationToken)
                ?? throw new NotFoundException("Trip not found.");

            var slot = await db.TripDateSlots
                .FromSqlInterpolated($"SELECT * FROM TripDateSlots WHERE TripDateSlotId = {booking.TripDateSlotId} FOR UPDATE")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Date slot not found.");

            if (request.DiscountAmount < 0)
            {
                throw new ValidationAppException(["Discount amount cannot be negative."]);
            }

            var totalAmount = trip.AmountPerPerson * booking.NumberOfSeats - request.DiscountAmount;
            if (totalAmount < 0)
            {
                throw new ValidationAppException(["Discount amount cannot exceed the trip total."]);
            }

            if (request.AdvanceAmount < 0 || request.AdvanceAmount > totalAmount)
            {
                throw new ValidationAppException([$"Advance amount must be between 0 and {totalAmount}."]);
            }

            var affectedRows = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TripDateSlots SET AvailableSeats = AvailableSeats - {booking.NumberOfSeats}, UpdatedAt = UTC_TIMESTAMP(6) WHERE TripDateSlotId = {slot.TripDateSlotId} AND AvailableSeats >= {booking.NumberOfSeats}",
                cancellationToken);

            if (affectedRows != 1)
            {
                throw new InsufficientSeatsException(slot.AvailableSeats);
            }

            var now = DateTime.UtcNow;
            booking.AmountPerPerson = trip.AmountPerPerson;
            booking.TotalAmount = totalAmount;
            booking.AdvanceAmount = request.AdvanceAmount;
            booking.RemainingAmount = totalAmount - request.AdvanceAmount;
            booking.BookingStatus = BookingStatus.Confirmed;
            booking.PaymentStatus = ComputePaymentStatus(request.AdvanceAmount, totalAmount);
            booking.ConfirmedAt = now;
            booking.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(request.RazorpayPaymentId))
            {
                booking.RazorpayPaymentId = request.RazorpayPaymentId;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Outside the transaction on purpose — a WhatsApp failure must never roll back a
            // successful, already-committed seat confirmation.
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == booking.CustomerId, cancellationToken);
            if (customer is not null)
            {
                await NotifyBookingConfirmedAsync(booking, trip, slot, customer, cancellationToken);
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RejectBookingAsync(int bookingId, RejectBookingRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending))
            {
                throw new ConflictException("Only requested or pending bookings can be rejected.");
            }

            var now = DateTime.UtcNow;
            booking.BookingStatus = BookingStatus.Rejected;
            booking.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.AdminNotes))
            {
                booking.AdminNotes = request.AdminNotes;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task CancelBookingAsync(int bookingId, CancelBookingRequest request, CancellationToken cancellationToken = default)
    {
        // A lightweight read first — no lock yet — just to give a clear rejection before we'd ever
        // touch Razorpay or the guarded seat-restore path. Mirrors CancelOwnBookingAsync below.
        var preCheck = await db.Bookings
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (preCheck.BookingStatus != BookingStatus.Confirmed)
        {
            throw new ConflictException("Only confirmed bookings can be cancelled.");
        }

        var refunded = await TryRefundOnlinePaymentAsync(
            preCheck,
            bookingId,
            "We couldn't process the Razorpay refund right now. Please try again shortly, or refund manually and re-run cancellation once done.",
            cancellationToken);

        var adminNotes = refunded
            ? AppendNote(request.AdminNotes, "Refunded in full via Razorpay.")
            : request.AdminNotes;

        await CancelInternalAsync(bookingId, adminNotes, cancellationToken);
    }

    public async Task<BookingResponseDto> CancelOwnBookingAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        // A lightweight read first — no lock yet — just to give a clear rejection before we'd ever
        // touch Razorpay or the guarded seat-restore path.
        var preCheck = await db.Bookings
            .Include(b => b.TripDateSlot)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (preCheck.BookingStatus is BookingStatus.Requested or BookingStatus.Pending)
        {
            // Nothing was ever charged (AdvanceAmount is always 0 pre-confirm) and no seats were
            // ever deducted — withdrawing the request is just a status flip, no refund/restore needed.
            await WithdrawRequestedInternalAsync(bookingId, "Withdrawn by customer before confirmation.", cancellationToken);
            return await GetCustomerBookingDetailAsync(customerId, bookingId, cancellationToken);
        }

        if (preCheck.BookingStatus != BookingStatus.Confirmed)
        {
            throw new ConflictException("This booking can no longer be cancelled.");
        }

        var tripStartUtc = preCheck.TripDateSlot.StartDate.ToDateTime(TimeOnly.MinValue);
        if (DateTime.UtcNow > tripStartUtc.AddHours(-CancellationWindowHours))
        {
            throw new ConflictException($"Cancellations are only allowed up to {CancellationWindowHours} hours before the trip starts. Please contact us directly.");
        }

        var refunded = await TryRefundOnlinePaymentAsync(
            preCheck,
            bookingId,
            "We couldn't process your refund right now. Please try again shortly or contact us.",
            cancellationToken);

        var adminNotes = refunded ? "Cancelled by customer — refunded in full via Razorpay." : "Cancelled by customer.";

        await CancelInternalAsync(bookingId, adminNotes, cancellationToken);
        return await GetCustomerBookingDetailAsync(customerId, bookingId, cancellationToken);
    }

    /// <summary>
    /// Refunds the booking's Razorpay payment in full, if one exists. Used by both the customer
    /// self-cancel and admin-cancel paths so a booking is never marked Refunded without the gateway
    /// actually having released the money — on failure this blocks cancellation entirely rather than
    /// leaving payment/seat state inconsistent with what the database claims happened.
    /// </summary>
    private async Task<bool> TryRefundOnlinePaymentAsync(Booking booking, int bookingId, string failureMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(booking.RazorpayPaymentId) || booking.AdvanceAmount <= 0)
        {
            return false;
        }

        try
        {
            await razorpay.RefundAsync(booking.RazorpayPaymentId, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is PaymentGatewayException or PaymentGatewayAuthException)
        {
            logger.LogError(ex, "Refund failed for booking {BookingId} — cancellation blocked so payment and seat state stay consistent.", bookingId);
            throw new ConflictException(failureMessage);
        }
    }

    private static string? AppendNote(string? existing, string note) =>
        string.IsNullOrWhiteSpace(existing) ? note : $"{existing} — {note}";

    private async Task CancelInternalAsync(int bookingId, string? adminNotes, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus != BookingStatus.Confirmed)
            {
                throw new ConflictException("Only confirmed bookings can be cancelled.");
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TripDateSlots SET AvailableSeats = LEAST(AvailableSeats + {booking.NumberOfSeats}, TotalSeats), UpdatedAt = UTC_TIMESTAMP(6) WHERE TripDateSlotId = {booking.TripDateSlotId}",
                cancellationToken);

            var now = DateTime.UtcNow;
            booking.BookingStatus = BookingStatus.Cancelled;
            booking.PaymentStatus = PaymentStatus.Refunded;
            booking.CancelledAt = now;
            booking.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(adminNotes))
            {
                booking.AdminNotes = adminNotes;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task WithdrawRequestedInternalAsync(int bookingId, string adminNotes, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // FOR UPDATE here isn't guarding a seat count — it's what makes this safe against an
            // admin confirming the same booking in the same instant: whichever transaction gets
            // the row lock first decides the outcome, and the status re-check below rejects the loser.
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending))
            {
                throw new ConflictException("This booking can no longer be cancelled.");
            }

            var now = DateTime.UtcNow;
            booking.BookingStatus = BookingStatus.Cancelled;
            booking.CancelledAt = now;
            booking.UpdatedAt = now;
            booking.AdminNotes = adminNotes;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> PromoteCompletedBookingsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var bookingsToComplete = await db.Bookings
            .Include(b => b.TripDateSlot)
            .Where(b => b.BookingStatus == BookingStatus.Confirmed && b.TripDateSlot.EndDate < today)
            .ToListAsync(cancellationToken);

        if (bookingsToComplete.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var booking in bookingsToComplete)
        {
            booking.BookingStatus = BookingStatus.Completed;
            booking.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return bookingsToComplete.Count;
    }

    // ---------- Helpers ----------

    private static PaymentStatus ComputePaymentStatus(decimal advanceAmount, decimal totalAmount)
    {
        if (advanceAmount <= 0)
        {
            return PaymentStatus.Unpaid;
        }

        return advanceAmount >= totalAmount ? PaymentStatus.Paid : PaymentStatus.AdvancePaid;
    }

    private static string BuildWhatsAppMessage(Booking booking, Trip trip, TripDateSlot slot, Customer customer) =>
        $"Hi Ghumo Odisha, I'd like to book {booking.NumberOfSeats} seat(s) for {trip.Title} " +
        $"({slot.StartDate:dd MMM yyyy} - {slot.EndDate:dd MMM yyyy}). " +
        $"Total: Rs {booking.TotalAmount:N0}. Booking ID: GO-{booking.BookingId}. " +
        $"My name is {customer.Name}, phone {customer.PhoneNumber}.";

    // ---------- WhatsApp notifications ----------
    //
    // Fast2SMS has exactly one approved template on this account, built for OTP delivery with two
    // variable slots. There is no separate approved template for booking notices, so these reuse
    // that same template — variable 1 as a short "who this is for" line, variable 2 packed with the
    // booking summary. Whatever fixed wrapper text Meta approved around those two slots (e.g. "Your
    // OTP is {{2}}") still applies and cannot be changed here; if it reads oddly to customers, the
    // real fix is getting a dedicated booking-notice template approved and swapping the MessageId.
    //
    // These are always best-effort: a WhatsApp failure must never fail a booking request/confirm/cancel.

    private async Task NotifyBookingRequestedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer, CancellationToken cancellationToken)
    {
        var summary = $"Booking GO-{booking.BookingId} requested: {trip.Title}, " +
                      $"{slot.StartDate:dd MMM}-{slot.EndDate:dd MMM}, {booking.NumberOfSeats} seat(s), " +
                      $"Rs {booking.TotalAmount:N0}. We'll confirm shortly.";
        await TrySendAsync(customer.PhoneNumber, customer.Name, summary, "booking-requested/customer", cancellationToken);

        if (!string.IsNullOrWhiteSpace(_organizerContact.WhatsAppNumber))
        {
            var adminSummary = $"GO-{booking.BookingId}: {customer.Name} ({customer.PhoneNumber}) - {trip.Title}, " +
                               $"{booking.NumberOfSeats} seat(s), Rs {booking.TotalAmount:N0}. Status: Requested.";
            await TrySendAsync(_organizerContact.WhatsAppNumber, "New Booking Request", adminSummary, "booking-requested/admin", cancellationToken);
        }
    }

    private async Task NotifyBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer, CancellationToken cancellationToken)
    {
        var balanceNote = booking.RemainingAmount > 0 ? $", balance Rs {booking.RemainingAmount:N0} due before the trip" : ", fully paid";
        var summary = $"Booking GO-{booking.BookingId} CONFIRMED: {trip.Title}, " +
                      $"{slot.StartDate:dd MMM}-{slot.EndDate:dd MMM}, {booking.NumberOfSeats} seat(s). " +
                      $"Paid Rs {booking.AdvanceAmount:N0}{balanceNote}. See you there!";
        await TrySendAsync(customer.PhoneNumber, customer.Name, summary, "booking-confirmed/customer", cancellationToken);

        if (!string.IsNullOrWhiteSpace(_organizerContact.WhatsAppNumber))
        {
            var adminSummary = $"GO-{booking.BookingId}: {customer.Name} ({customer.PhoneNumber}) - {trip.Title}, " +
                               $"paid Rs {booking.AdvanceAmount:N0}, balance Rs {booking.RemainingAmount:N0}. Status: Confirmed.";
            await TrySendAsync(_organizerContact.WhatsAppNumber, "Booking Confirmed", adminSummary, "booking-confirmed/admin", cancellationToken);
        }
    }

    private async Task TrySendAsync(string phoneNumber, string variable1, string variable2, string context, CancellationToken cancellationToken)
    {
        try
        {
            await whatsApp.SendTemplateAsync(phoneNumber, variable1, variable2, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp notification failed ({Context}) — continuing without blocking the booking operation.", context);
        }
    }

    private static string? CoverImageUrl(IEnumerable<TripPhoto> photos) =>
        photos.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.ImageUrl;

    private static BookingResponseDto MapToResponse(Booking booking, string tripTitle, string? tripCoverImageUrl, TripDateSlot slot, PickupPoint? pickupPoint) => new(
        booking.BookingId,
        booking.TripId,
        tripTitle,
        tripCoverImageUrl,
        booking.TripDateSlotId,
        slot.StartDate,
        slot.EndDate,
        pickupPoint?.Location,
        pickupPoint?.Time,
        booking.NumberOfSeats,
        booking.AmountPerPerson,
        booking.TotalAmount,
        booking.AdvanceAmount,
        booking.RemainingAmount,
        booking.BookingStatus,
        booking.PaymentStatus,
        booking.BookingSource,
        booking.CustomerNotes,
        booking.AdminNotes,
        booking.RequestedAt,
        booking.ConfirmedAt,
        booking.CancelledAt);

    private static AdminBookingListItemDto MapToAdminListItem(Booking booking) => new(
        booking.BookingId,
        booking.CustomerId,
        booking.Customer.Name,
        booking.Customer.PhoneNumber,
        booking.TripId,
        booking.Trip.Title,
        booking.TripDateSlotId,
        booking.TripDateSlot.StartDate,
        booking.TripDateSlot.EndDate,
        booking.NumberOfSeats,
        booking.TotalAmount,
        booking.AdvanceAmount,
        booking.BookingStatus,
        booking.PaymentStatus,
        booking.BookingSource,
        booking.RequestedAt);
}
