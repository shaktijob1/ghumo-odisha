using System.Data;
using System.Globalization;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Legal;
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
    IWhatsAppService whatsApp,
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

        if (!request.AgreedToTerms)
        {
            throw new ValidationAppException(["You must accept the Terms & Conditions to request a booking."]);
        }

        var trip = await db.Trips.Include(t => t.TripPhotos).FirstOrDefaultAsync(t => t.TripId == request.TripId && t.Status == TripStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == request.TripDateSlotId && s.TripId == request.TripId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        SlotBookingRules.EnsureBookable(slot, request.NumberOfSeats);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        // A customer requesting the same date slot again (e.g. after changing seat count) should
        // leave only the newest request standing — supersede whatever they still had pending for
        // this exact slot rather than piling up duplicate Requested rows an admin would have to sort out.
        var priorRequestsForSlot = await db.Bookings
            .Where(b => b.CustomerId == customerId
                     && b.TripDateSlotId == request.TripDateSlotId
                     && (b.BookingStatus == BookingStatus.Requested || b.BookingStatus == BookingStatus.Pending))
            .ToListAsync(cancellationToken);

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

        foreach (var prior in priorRequestsForSlot)
        {
            prior.BookingStatus = BookingStatus.Cancelled;
            prior.CancelledAt = now;
            prior.UpdatedAt = now;
            prior.AdminNotes = AppendNote(prior.AdminNotes, "Cancelled automatically — replaced by a newer request for the same date.");
            BookingTimeline.Add(db, prior, BookingEventType.Cancelled, "Request replaced",
                "Replaced by a newer request for the same date.", BookingTimeline.System, at: now);
        }

        db.Bookings.Add(booking);
        BookingTimeline.Add(db, booking, BookingEventType.Requested, "Booking requested",
            $"{request.NumberOfSeats} seat(s) for {slot.StartDate:d MMM yyyy} · Total {BookingTimeline.Money(totalAmount)}",
            BookingTimeline.Customer, at: now);

        // Snapshotted verbatim from the canonical text at this exact moment — never re-read later,
        // so a future wording change can never retroactively alter what this customer agreed to.
        db.TermsAcceptances.Add(new TermsAcceptance
        {
            CustomerId = customerId,
            Booking = booking,
            Version = TermsAndConditionsContent.Version,
            Text = TermsAndConditionsContent.Text,
            AcceptedAt = now
        });

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

        // No WhatsApp is sent here — the booking is only Requested, nothing has been paid yet.
        // The first message a customer/organizer gets is on confirmation (NotifyBookingConfirmedAsync),
        // once an advance has actually been paid.
        var message = BuildWhatsAppMessage(booking, trip, slot, customer);
        return new CreateBookingResult(MapToResponse(booking, trip.Title, CoverImageUrl(trip.TripPhotos), slot, pickupPoint, isOwner: true, [], []), message);
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
        return new CreateBookingResult(MapToResponse(existing, existing.Trip.Title, CoverImageUrl(existing.Trip.TripPhotos), existing.TripDateSlot, existing.PickupPoint, isOwner: true, [], []), message);
    }

    /// <summary>Bookings this customer made, plus ones the organizer added them to as a traveller (view-only).</summary>
    private IQueryable<Booking> VisibleToCustomer(int customerId) =>
        db.Bookings.Where(b => b.CustomerId == customerId || b.Travellers.Any(t => t.LinkedCustomerId == customerId));

    public async Task<PagedResult<BookingResponseDto>> GetCustomerBookingsAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = VisibleToCustomer(customerId).OrderByDescending(b => b.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var bookings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.Trip).ThenInclude(t => t.TripPhotos)
            .Include(b => b.TripDateSlot)
            .Include(b => b.PickupPoint)
            .ToListAsync(cancellationToken);

        var timelines = await GetCustomerTimelinesAsync(bookings.Select(b => b.BookingId).ToList(), cancellationToken);
        var payments = await GetCustomerPaymentsAsync(bookings.Where(b => b.CustomerId == customerId).Select(b => b.BookingId).ToList(), cancellationToken);
        var items = bookings.Select(b => MapToResponse(b, b.Trip.Title, CoverImageUrl(b.Trip.TripPhotos), b.TripDateSlot, b.PickupPoint,
            isOwner: b.CustomerId == customerId, timelines.GetValueOrDefault(b.BookingId, []), payments.GetValueOrDefault(b.BookingId, []))).ToList();
        return new PagedResult<BookingResponseDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<BookingResponseDto> GetCustomerBookingDetailAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await VisibleToCustomer(customerId)
            .Include(b => b.Trip).ThenInclude(t => t.TripPhotos)
            .Include(b => b.TripDateSlot)
            .Include(b => b.PickupPoint)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        var timelines = await GetCustomerTimelinesAsync([bookingId], cancellationToken);
        var payments = booking.CustomerId == customerId ? await GetCustomerPaymentsAsync([bookingId], cancellationToken) : new();
        return MapToResponse(booking, booking.Trip.Title, CoverImageUrl(booking.Trip.TripPhotos), booking.TripDateSlot, booking.PickupPoint,
            isOwner: booking.CustomerId == customerId, timelines.GetValueOrDefault(bookingId, []), payments.GetValueOrDefault(bookingId, []));
    }

    private async Task<Dictionary<int, IReadOnlyList<BookingEventDto>>> GetCustomerTimelinesAsync(IReadOnlyList<int> bookingIds, CancellationToken cancellationToken)
    {
        var events = await db.BookingEvents.AsNoTracking()
            .Where(e => bookingIds.Contains(e.BookingId) && e.IsVisibleToCustomer)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.BookingEventId)
            .ToListAsync(cancellationToken);

        return events.GroupBy(e => e.BookingId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BookingEventDto>)g.Select(MapEvent).ToList());
    }

    private async Task<Dictionary<int, IReadOnlyList<BookingPaymentDto>>> GetCustomerPaymentsAsync(IReadOnlyList<int> bookingIds, CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0)
        {
            return new();
        }

        var payments = await db.BookingPayments.AsNoTracking()
            .Where(p => bookingIds.Contains(p.BookingId))
            .OrderBy(p => p.PaidAt).ThenBy(p => p.BookingPaymentId)
            .ToListAsync(cancellationToken);

        return payments.GroupBy(p => p.BookingId).ToDictionary(g => g.Key, g => (IReadOnlyList<BookingPaymentDto>)g
            .Select(p => new BookingPaymentDto(p.BookingPaymentId, p.Amount, p.Method, p.Reference, p.Notes, p.RecordedBy, p.PaidAt))
            .ToList());
    }

    private static BookingEventDto MapEvent(BookingEvent e) => new(e.EventType, e.Title, e.Description, e.Actor, e.CreatedAt);

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
        var payments = await GetPaymentDtosAsync(bookingId, cancellationToken);
        var couponCode = await db.CouponRedemptions
            .Where(r => r.BookingId == bookingId)
            .Select(r => r.CouponCode.Code)
            .FirstOrDefaultAsync(cancellationToken);
        var travellers = await db.BookingTravellers.AsNoTracking()
            .Where(t => t.BookingId == bookingId)
            .OrderBy(t => t.SeatNumber)
            .Select(t => new AdminTravellerDto(t.BookingTravellerId, t.SeatNumber, t.FullName, t.Gender, t.Age, t.AadhaarLast4,
                t.PhoneNumber, t.LinkedCustomerId, t.LinkedCustomer != null ? t.LinkedCustomer.Name : null))
            .ToListAsync(cancellationToken);
        var timeline = await db.BookingEvents.AsNoTracking()
            .Where(e => e.BookingId == bookingId)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.BookingEventId)
            .ToListAsync(cancellationToken);

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
            booking.CancelledAt,
            booking.DiscountAmount,
            couponCode,
            payments,
            RoomAllocation.ForSeats(booking.NumberOfSeats),
            booking.MaleCount,
            booking.FemaleCount,
            booking.CancellationReason,
            booking.RefundWaived,
            travellers,
            timeline.Select(MapEvent).ToList());
    }

    private async Task<IReadOnlyList<BookingPaymentDto>> GetPaymentDtosAsync(int bookingId, CancellationToken cancellationToken) =>
        await db.BookingPayments.AsNoTracking()
            .Where(p => p.BookingId == bookingId)
            .OrderBy(p => p.PaidAt).ThenBy(p => p.BookingPaymentId)
            .Select(p => new BookingPaymentDto(p.BookingPaymentId, p.Amount, p.Method, p.Reference, p.Notes, p.RecordedBy, p.PaidAt))
            .ToListAsync(cancellationToken);

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

        SlotBookingRules.EnsureBookable(slot, request.NumberOfSeats);

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
        BookingTimeline.Add(db, booking, BookingEventType.Created, "Booking created by Ghumo Odisha",
            $"{request.NumberOfSeats} seat(s) for {slot.StartDate:d MMM yyyy} · booked via {request.BookingSource}",
            BookingTimeline.Admin, at: bookingNow);
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

            if (slot.Status != TripDateSlotStatus.Active || TripCalendar.HasDeparted(slot.StartDate))
            {
                throw new DepartureClosedException();
            }

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

            await DeductSeatsAsync(slot, booking.NumberOfSeats, cancellationToken);

            var now = DateTime.UtcNow;
            booking.AmountPerPerson = trip.AmountPerPerson;
            booking.TotalAmount = totalAmount;
            booking.DiscountAmount = request.DiscountAmount;
            booking.AdvanceAmount = request.AdvanceAmount;
            if (request.AdvanceAmount > 0)
            {
                var isOnline = !string.IsNullOrWhiteSpace(request.RazorpayPaymentId) || request.Method == PaymentMethod.Razorpay;
                db.BookingPayments.Add(new BookingPayment
                {
                    BookingId = booking.BookingId,
                    Amount = request.AdvanceAmount,
                    Method = isOnline ? PaymentMethod.Razorpay : request.Method ?? PaymentMethod.Cash,
                    Reference = request.RazorpayPaymentId ?? request.PaymentReference,
                    Notes = isOnline ? "Booking advance paid online" : "Advance collected at confirmation",
                    RecordedBy = isOnline ? PaymentRecordedBy.Customer : PaymentRecordedBy.Admin,
                    PaidAt = now,
                    CreatedAt = now
                });
            }
            booking.RemainingAmount = totalAmount - request.AdvanceAmount;
            booking.BookingStatus = BookingStatus.Confirmed;
            booking.PaymentStatus = ComputePaymentStatus(request.AdvanceAmount, totalAmount);
            booking.ConfirmedAt = now;
            booking.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(request.RazorpayPaymentId))
            {
                booking.RazorpayPaymentId = request.RazorpayPaymentId;
            }

            var paidOnline = !string.IsNullOrWhiteSpace(request.RazorpayPaymentId) || request.Method == PaymentMethod.Razorpay;
            var advanceNote = request.AdvanceAmount > 0
                ? $" · {BookingTimeline.Money(request.AdvanceAmount)} paid {BookingTimeline.MethodLabel(paidOnline ? PaymentMethod.Razorpay : request.Method ?? PaymentMethod.Cash)}"
                : "";
            BookingTimeline.Add(db, booking, BookingEventType.Confirmed, "Booking confirmed",
                $"{booking.NumberOfSeats} seat(s) reserved{advanceNote} · Balance {BookingTimeline.Money(booking.RemainingAmount)}",
                paidOnline ? BookingTimeline.Customer : BookingTimeline.Admin, at: now);

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

    public async Task AddPaymentAsync(int bookingId, AddBookingPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Locked so two admins recording the same cash at once can't both pass the balance check.
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Confirmed or BookingStatus.Completed))
            {
                throw new ConflictException("Payments can only be added to a confirmed booking.");
            }

            var amount = decimal.Round(request.Amount, 2);
            if (amount <= 0)
            {
                throw new ValidationAppException(["Amount must be greater than zero."]);
            }

            if (amount > booking.RemainingAmount)
            {
                throw new ValidationAppException([$"Amount can't be more than the balance due of Rs {booking.RemainingAmount:N2}."]);
            }

            var now = DateTime.UtcNow;
            db.BookingPayments.Add(new BookingPayment
            {
                BookingId = booking.BookingId,
                Amount = amount,
                Method = request.Method,
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                RecordedBy = PaymentRecordedBy.Admin,
                PaidAt = request.PaidAt?.ToUniversalTime() ?? now,
                CreatedAt = now
            });

            ApplyAmountPaid(booking, booking.AdvanceAmount + amount, now);
            BookingTimeline.Add(db, booking, BookingEventType.PaymentReceived, "Payment received",
                $"{BookingTimeline.Money(amount)} via {BookingTimeline.MethodLabel(request.Method)} · " +
                (booking.RemainingAmount > 0 ? $"Balance {BookingTimeline.Money(booking.RemainingAmount)}" : "Fully paid"),
                BookingTimeline.Admin, at: now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RemovePaymentAsync(int bookingId, int bookingPaymentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Confirmed or BookingStatus.Completed))
            {
                throw new ConflictException("Payments can only be changed on a confirmed booking.");
            }

            var payment = await db.BookingPayments
                .FirstOrDefaultAsync(p => p.BookingPaymentId == bookingPaymentId && p.BookingId == bookingId, cancellationToken)
                ?? throw new NotFoundException("Payment not found.");

            // Money taken online can only go back to the customer through a Razorpay refund (cancel),
            // never by deleting the record of it.
            if (payment.Method == PaymentMethod.Razorpay)
            {
                throw new ConflictException("Online payments can't be removed — cancel the booking to refund them.");
            }

            db.BookingPayments.Remove(payment);
            ApplyAmountPaid(booking, booking.AdvanceAmount - payment.Amount, DateTime.UtcNow);
            BookingTimeline.Add(db, booking, BookingEventType.PaymentRemoved, "Payment entry removed",
                $"{BookingTimeline.Money(payment.Amount)} {BookingTimeline.MethodLabel(payment.Method)} entry from {payment.PaidAt:d MMM yyyy} removed · Balance {BookingTimeline.Money(booking.RemainingAmount)}",
                BookingTimeline.Admin, visibleToCustomer: false);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ChangeSeatsAsync(int bookingId, ChangeSeatsRequest request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationAppException(["A reason is required to change the seats."]);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending or BookingStatus.Confirmed))
            {
                throw new ConflictException("Seats can only be changed on a requested, pending or confirmed booking.");
            }

            var oldSeats = booking.NumberOfSeats;
            var newSeats = request.NumberOfSeats;
            if (newSeats < 1)
            {
                throw new ValidationAppException(["A booking needs at least 1 seat — cancel it instead."]);
            }

            if (newSeats == oldSeats)
            {
                throw new ValidationAppException([$"This booking already has {oldSeats} seat(s)."]);
            }

            var travellersOnDroppedSeats = await db.BookingTravellers
                .Where(t => t.BookingId == bookingId && t.SeatNumber > newSeats)
                .Select(t => t.SeatNumber)
                .OrderBy(n => n)
                .ToListAsync(cancellationToken);
            if (travellersOnDroppedSeats.Count > 0)
            {
                throw new ConflictException(
                    $"Seat(s) {string.Join(", ", travellersOnDroppedSeats)} still have traveller details. Remove those travellers first.");
            }

            var slot = await db.TripDateSlots
                .FromSqlInterpolated($"SELECT * FROM TripDateSlots WHERE TripDateSlotId = {booking.TripDateSlotId} FOR UPDATE")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Date slot not found.");

            var delta = newSeats - oldSeats;
            if (booking.BookingStatus == BookingStatus.Confirmed)
            {
                // A confirmed booking already holds its seats: take or give back only the difference,
                // through the same guarded paths Confirm and Cancel use.
                if (delta > 0)
                {
                    if (slot.Status != TripDateSlotStatus.Active || TripCalendar.HasDeparted(slot.StartDate))
                    {
                        throw new DepartureClosedException();
                    }

                    await DeductSeatsAsync(slot, delta, cancellationToken);
                }
                else
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE TripDateSlots SET AvailableSeats = LEAST(AvailableSeats + {-delta}, TotalSeats), UpdatedAt = UTC_TIMESTAMP(6) WHERE TripDateSlotId = {slot.TripDateSlotId}",
                        cancellationToken);
                }
            }
            else if (delta > 0)
            {
                // Not confirmed yet, so nothing is deducted — but don't let it grow past what could ever be confirmed.
                SlotBookingRules.EnsureBookable(slot, newSeats);
            }

            var now = DateTime.UtcNow;
            booking.NumberOfSeats = newSeats;
            booking.TotalAmount = Math.Max(0, booking.AmountPerPerson * newSeats - booking.DiscountAmount);
            booking.RemainingAmount = Math.Max(0, booking.TotalAmount - booking.AdvanceAmount);
            if (booking.BookingStatus == BookingStatus.Confirmed)
            {
                booking.PaymentStatus = ComputePaymentStatus(booking.AdvanceAmount, booking.TotalAmount);
            }
            else
            {
                // Any Razorpay order quoted for the old seat count is void — the next payment attempt re-quotes.
                booking.RazorpayOrderId = null;
                booking.PendingAdvanceAmount = null;
                booking.PendingDiscountAmount = null;
                booking.PendingCouponCodeId = null;
                booking.PendingCouponDiscountAmount = null;
            }
            booking.UpdatedAt = now;

            var genderNote = "";
            if ((booking.MaleCount ?? 0) + (booking.FemaleCount ?? 0) > newSeats)
            {
                booking.MaleCount = null;
                booking.FemaleCount = null;
                genderNote = " · Male/female count cleared — please re-enter";
            }

            // Commission follows the seats actually travelling, at the per-seat rate snapshotted at redemption.
            var redemption = await db.CouponRedemptions.FirstOrDefaultAsync(r => r.BookingId == bookingId, cancellationToken);
            if (redemption is not null && redemption.NumberOfSeats > 0)
            {
                var perSeat = redemption.CommissionAmount / redemption.NumberOfSeats;
                redemption.NumberOfSeats = newSeats;
                redemption.CommissionAmount = decimal.Round(perSeat * newSeats, 2);
            }

            var excess = booking.AdvanceAmount - booking.TotalAmount;
            var excessNote = excess > 0 ? $" · {BookingTimeline.Money(excess)} already paid above the new total is non-refundable" : "";
            BookingTimeline.Add(db, booking, BookingEventType.SeatsChanged, $"Seats changed from {oldSeats} to {newSeats}",
                $"New total {BookingTimeline.Money(booking.TotalAmount)} · {RoomAllocation.ForSeats(newSeats)} room(s) · Reason: {reason}{excessNote}{genderNote}",
                BookingTimeline.Admin, at: now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void ApplyAmountPaid(Booking booking, decimal amountPaid, DateTime now)
    {
        booking.AdvanceAmount = amountPaid;
        // Floors at zero: after a seat reduction the customer may have paid more than the new total (no refunds).
        booking.RemainingAmount = Math.Max(0, booking.TotalAmount - amountPaid);
        booking.PaymentStatus = ComputePaymentStatus(amountPaid, booking.TotalAmount);
        booking.UpdatedAt = now;
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

            BookingTimeline.Add(db, booking, BookingEventType.Rejected, "Booking request declined",
                "This request couldn't be accepted. No payment was taken.", BookingTimeline.Admin, at: now);

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

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (request.WaiveRefund)
        {
            // Admin's call to keep what was paid (e.g. a late cancellation) — Razorpay is never touched.
            if (reason is null)
            {
                throw new ValidationAppException(["A reason is required to cancel without a refund."]);
            }

            await CancelInternalAsync(bookingId, AppendNote(request.AdminNotes, $"Cancelled without refund: {reason}"),
                refundId: null, BookingTimeline.Admin, reason, refundWaived: true, cancellationToken);
            return;
        }

        var refundId = await TryRefundOnlinePaymentAsync(
            preCheck,
            bookingId,
            "We couldn't process the Razorpay refund right now. Please try again shortly, or refund manually and re-run cancellation once done.",
            cancellationToken);

        var adminNotes = refundId is not null
            ? AppendNote(request.AdminNotes, "Refunded in full via Razorpay.")
            : request.AdminNotes;

        await CancelInternalAsync(bookingId, adminNotes, refundId, BookingTimeline.Admin, reason, refundWaived: false, cancellationToken);
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

        var refundId = await TryRefundOnlinePaymentAsync(
            preCheck,
            bookingId,
            "We couldn't process your refund right now. Please try again shortly or contact us.",
            cancellationToken);

        var adminNotes = refundId is not null ? "Cancelled by customer — refunded in full via Razorpay." : "Cancelled by customer.";

        await CancelInternalAsync(bookingId, adminNotes, refundId, BookingTimeline.Customer, reason: null, refundWaived: false, cancellationToken);
        return await GetCustomerBookingDetailAsync(customerId, bookingId, cancellationToken);
    }

    /// <summary>
    /// Refunds the booking's Razorpay payment in full, if one exists, and returns the created
    /// refund's id (null if no online payment was ever taken). Used by both the customer self-cancel
    /// and admin-cancel paths so a booking is never marked Refunded without the gateway actually
    /// having released the money — on failure this blocks cancellation entirely rather than leaving
    /// payment/seat state inconsistent with what the database claims happened.
    /// </summary>
    private async Task<string?> TryRefundOnlinePaymentAsync(Booking booking, int bookingId, string failureMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(booking.RazorpayPaymentId) || booking.AdvanceAmount <= 0)
        {
            return null;
        }

        try
        {
            return await razorpay.RefundAsync(booking.RazorpayPaymentId, cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayException or PaymentGatewayAuthException)
        {
            logger.LogError(ex, "Refund failed for booking {BookingId} — cancellation blocked so payment and seat state stay consistent.", bookingId);
            throw new ConflictException(failureMessage);
        }
    }

    /// <summary>
    /// The only code that ever decreases AvailableSeats. Must run inside the caller's transaction,
    /// after the slot row has been locked with SELECT ... FOR UPDATE. The WHERE guard makes an
    /// oversell impossible even if the caller's own availability check raced another booking.
    /// </summary>
    private async Task DeductSeatsAsync(TripDateSlot lockedSlot, int seats, CancellationToken cancellationToken)
    {
        var affectedRows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TripDateSlots SET AvailableSeats = AvailableSeats - {seats}, UpdatedAt = UTC_TIMESTAMP(6) WHERE TripDateSlotId = {lockedSlot.TripDateSlotId} AND AvailableSeats >= {seats}",
            cancellationToken);

        if (affectedRows != 1)
        {
            throw new InsufficientSeatsException(lockedSlot.AvailableSeats);
        }
    }

    private static string? AppendNote(string? existing, string note) =>
        string.IsNullOrWhiteSpace(existing) ? note : $"{existing} — {note}";

    private async Task CancelInternalAsync(int bookingId, string? adminNotes, string? refundId, string actor, string? reason,
        bool refundWaived, CancellationToken cancellationToken)
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
            // Waived: what was paid stays paid (and is shown that way); otherwise it's refunded.
            if (!refundWaived)
            {
                booking.PaymentStatus = PaymentStatus.Refunded;
            }
            booking.RefundWaived = refundWaived;
            booking.CancellationReason = reason;
            booking.CancelledAt = now;
            booking.UpdatedAt = now;
            if (refundId is not null)
            {
                booking.RazorpayRefundId = refundId;
            }
            if (!string.IsNullOrWhiteSpace(adminNotes))
            {
                booking.AdminNotes = adminNotes;
            }

            string refundNote;
            if (refundWaived)
            {
                refundNote = booking.AdvanceAmount > 0 ? $"No refund — {BookingTimeline.Money(booking.AdvanceAmount)} paid is retained" : "No payment was made";
            }
            else if (refundId is not null)
            {
                var onlinePaid = await db.BookingPayments
                    .Where(p => p.BookingId == bookingId && p.Method == PaymentMethod.Razorpay)
                    .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
                refundNote = $"{BookingTimeline.Money(onlinePaid)} refunded to the original payment method via Razorpay";
            }
            else
            {
                refundNote = booking.AdvanceAmount > 0 ? $"{BookingTimeline.Money(booking.AdvanceAmount)} to be refunded by our team" : "No payment was made";
            }

            BookingTimeline.Add(db, booking, BookingEventType.Cancelled, "Booking cancelled",
                reason is null ? refundNote : $"{refundNote} · Reason: {reason}", actor, at: now);

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
            BookingTimeline.Add(db, booking, BookingEventType.Cancelled, "Request withdrawn",
                "Withdrawn before confirmation — nothing was charged.", BookingTimeline.Customer, at: now);

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
            BookingTimeline.Add(db, booking, BookingEventType.Completed, "Trip completed",
                "Hope you had a great trip with Ghumo Odisha!", BookingTimeline.System, at: now);
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
    // The customer's booking-confirmed notice uses its own approved 9-variable template
    // (WhatsApp:BookingConfirmedTemplateName). Everything else — the organizer's copy, and the
    // customer's notice when that template isn't configured — reuses the two-variable OTP template:
    // variable 1 as a short "who this is for" line, variable 2 packed with the booking summary.
    //
    // These are always best-effort: a WhatsApp failure must never fail a booking request/confirm/cancel.

    // Covers every way a booking becomes Confirmed — the admin's Confirm button (offline payment)
    // and a verified Razorpay advance — since both go through ConfirmBookingAsync.
    private async Task NotifyBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer, CancellationToken cancellationToken)
    {
        var sentDedicatedTemplate = false;
        try
        {
            var message = await BuildBookingConfirmedMessageAsync(booking, trip, slot, customer, cancellationToken);
            sentDedicatedTemplate = await whatsApp.SendBookingConfirmedAsync(customer.PhoneNumber, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp notification failed (booking-confirmed/customer) — continuing without blocking the booking operation.");
            sentDedicatedTemplate = true; // Attempted and failed — don't also send the fallback text.
        }

        if (!sentDedicatedTemplate)
        {
            // Dedicated template not configured yet — fall back to the shared two-variable template.
            var balanceNote = booking.RemainingAmount > 0 ? $", balance Rs {booking.RemainingAmount:N0} due before the trip" : ", fully paid";
            var summary = $"Booking GO-{booking.BookingId} CONFIRMED: {trip.Title}, " +
                          $"{slot.StartDate:dd MMM}-{slot.EndDate:dd MMM}, {booking.NumberOfSeats} seat(s). " +
                          $"Paid Rs {booking.AdvanceAmount:N0}{balanceNote}. See you there!";
            await TrySendAsync(customer.PhoneNumber, customer.Name, summary, "booking-confirmed/customer", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_organizerContact.WhatsAppNumber))
        {
            var adminSummary = $"GO-{booking.BookingId}: {customer.Name} ({customer.PhoneNumber}) - {trip.Title}, " +
                               $"paid Rs {booking.AdvanceAmount:N0}, balance Rs {booking.RemainingAmount:N0}. Status: Confirmed.";
            await TrySendAsync(_organizerContact.WhatsAppNumber, "Booking Confirmed", adminSummary, "booking-confirmed/admin", cancellationToken);
        }
    }

    private const string ToBeSharedText = "Will be shared by our team";

    private async Task<BookingConfirmedWhatsAppMessage> BuildBookingConfirmedMessageAsync(
        Booking booking, Trip trip, TripDateSlot slot, Customer customer, CancellationToken cancellationToken)
    {
        var pickupPoints = await db.PickupPoints.AsNoTracking()
            .Where(p => p.TripId == trip.TripId)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.PickupPointId)
            .ToListAsync(cancellationToken);

        // Pickup point = the trip's last pickup point in route order. Reporting time = the earliest
        // pickup time on the trip. Times are free text (the admin form saves "HH:mm"), so anything
        // that doesn't parse as a time is ignored; if none parse, the first point's text is used as-is.
        var lastPickupPoint = pickupPoints.LastOrDefault()?.Location;
        var earliestTime = pickupPoints
            .Select(p => TimeOnly.TryParse(p.Time, CultureInfo.InvariantCulture, out var t) ? t : (TimeOnly?)null)
            .Where(t => t.HasValue)
            .Min();
        var reportingTime = earliestTime?.ToString("h:mm tt", CultureInfo.InvariantCulture)
                            ?? pickupPoints.FirstOrDefault()?.Time;

        var name = string.IsNullOrWhiteSpace(customer.Name) ? "Traveller" : customer.Name.Trim();

        return new BookingConfirmedWhatsAppMessage(
            CustomerName: name,
            TripTitle: trip.Title,
            TravelDate: slot.StartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            PassengerName: name,
            Seats: booking.NumberOfSeats.ToString(CultureInfo.InvariantCulture),
            AmountPaid: booking.AdvanceAmount.ToString("#,##0.##", CultureInfo.InvariantCulture),
            BookingReference: $"GO-{booking.BookingId}",
            PickupPoint: string.IsNullOrWhiteSpace(lastPickupPoint) ? ToBeSharedText : lastPickupPoint,
            ReportingTime: string.IsNullOrWhiteSpace(reportingTime) ? ToBeSharedText : reportingTime);
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

    private static BookingResponseDto MapToResponse(Booking booking, string tripTitle, string? tripCoverImageUrl, TripDateSlot slot, PickupPoint? pickupPoint,
        bool isOwner, IReadOnlyList<BookingEventDto> timeline, IReadOnlyList<BookingPaymentDto> payments) => new(
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
        booking.CancelledAt,
        slot.AvailableSeats,
        isOwner,
        RoomAllocation.ForSeats(booking.NumberOfSeats),
        timeline,
        payments);

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
