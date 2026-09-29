using System.Data;
using System.Globalization;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Cars;

public interface ICarBookingService
{
    // Customer
    Task<CarBookingDto> CreateAsync(int customerId, CreateCarBookingRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarBookingDto>> ListForCustomerAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CarBookingDto> GetForCustomerAsync(int customerId, int carBookingId, CancellationToken cancellationToken = default);
    Task<CarBookingDto> CancelByCustomerAsync(int customerId, int carBookingId, string? reason, CancellationToken cancellationToken = default);

    // Driver
    Task<CarBookingDto> CancelByDriverAsync(int driverId, int carBookingId, string? reason, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResult<CarBookingSummaryDto>> ListAsync(CarBookingStatus? status, CarPaymentStatus? paymentStatus, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> GetForAdminAsync(int carBookingId, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> CancelByAdminAsync(int adminId, int carBookingId, AdminCancelCarBookingRequest request, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> UpdateNotesAsync(int carBookingId, string? notes, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> IssueRazorpayRefundAsync(int adminId, int carBookingId, decimal amount, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> RecordManualRefundAsync(int adminId, int carBookingId, RecordCarManualRefundRequest request, CancellationToken cancellationToken = default);
    Task<AdminCarBookingDetailDto> SettleRefundAsync(int adminId, int carBookingId, CancellationToken cancellationToken = default);

    // System
    Task<int> ExpireStaleHoldsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Car bookings. The one rule that matters: a car can never be double-booked. Creating a booking and
/// confirming a payment both lock the car row (SELECT … FOR UPDATE) inside a transaction and only then
/// check for overlapping bookings, so two customers racing for the same car serialize on that lock and
/// the second one sees the first one's booking. Fares are always computed here from the approved pricing —
/// nothing about price or distance-charge comes from the browser.
/// Cancelling never refunds automatically: a paid booking goes to RefundPending and the admin issues
/// the refund (same policy as trip bookings).
/// </summary>
public class CarBookingService(
    IGhumoOdishaDbContext db,
    IRazorpayService razorpay,
    IOptions<CarRentalOptions> options,
    ILogger<CarBookingService> logger) : ICarBookingService
{
    private readonly CarRentalOptions _options = options.Value;

    // =============== Customer ===============

    public async Task<CarBookingDto> CreateAsync(int customerId, CreateCarBookingRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            throw new ConflictException("Please add your WhatsApp number in your profile first, so your driver can reach you.");
        }
        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            throw new ConflictException("Please add your name in your profile first.");
        }

        // Retried request (double click, network retry): hand back the booking it already created.
        if (request.ClientRequestId is not null)
        {
            var existingId = await db.CarBookings
                .Where(b => b.CustomerId == customerId && b.ClientRequestId == request.ClientRequestId)
                .Select(b => (int?)b.CarBookingId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingId is not null)
            {
                return await GetForCustomerAsync(customerId, existingId.Value, cancellationToken);
            }
        }

        var now = DateTime.UtcNow;
        var window = CarRentalWindow.Resolve(request.PickupDate, request.PickupTime, request.DurationHours, _options, now);
        CarCatalogService.ValidateKm(request.EstimatedKm, _options);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockCarAsync(request.CarId, cancellationToken);

        var car = await db.Cars.AsNoTracking()
            .Listed()
            .Include(c => c.ActivePricing!).ThenInclude(p => p.Tiers)
            .FirstOrDefaultAsync(c => c.CarId == request.CarId, cancellationToken)
            ?? throw new ConflictException("This car isn't available for booking any more.");

        // An abandoned earlier attempt by the same customer shouldn't block this one (or anyone else).
        var abandoned = await db.CarBookings
            .Where(b => b.CustomerId == customerId && b.Status == CarBookingStatus.PendingPayment)
            .ToListAsync(cancellationToken);
        foreach (var old in abandoned)
        {
            old.Status = CarBookingStatus.Expired;
            old.HoldExpiresAt = null;
            old.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);

        if (await db.CarBookings.Where(b => b.CarId == car.CarId)
                .Overlapping(window.StartUtc, window.EndUtc, _options.TurnaroundBufferMinutes, now).AnyAsync(cancellationToken))
        {
            throw new ConflictException("Sorry, this car was just booked for part of that time. Please pick another time or car.");
        }

        var pricing = car.ActivePricing!;
        var fare = CarFareCalculator.Calculate(FareTerms.From(pricing), request.EstimatedKm, window.Nights);
        var booking = new CarBooking
        {
            BookingNumber = await BookingNumbers.NextAsync(db.CarBookings.Select(b => b.BookingNumber), cancellationToken),
            CustomerId = customerId,
            CarId = car.CarId,
            DriverId = car.DriverId,
            CarPricingId = pricing.CarPricingId,
            ClientRequestId = request.ClientRequestId,
            PickupCity = car.BaseCity,
            PickupAddress = Clean(request.PickupAddress, 500),
            PickupAt = window.StartUtc,
            DurationHours = window.DurationHours,
            EndsAt = window.EndUtc,
            EstimatedKm = request.EstimatedKm,
            EstimatedNights = window.Nights,
            EstimatedBaseFare = fare.BaseFare,
            EstimatedKmCharge = fare.KmCharge,
            EstimatedNightHaltCharge = fare.NightHaltCharge,
            EstimatedTotal = fare.Total,
            BookingAmount = _options.BookingAmount,
            Status = CarBookingStatus.PendingPayment,
            PaymentStatus = CarPaymentStatus.Unpaid,
            HoldExpiresAt = now.AddMinutes(_options.PaymentHoldMinutes),
            CustomerNotes = Clean(request.CustomerNotes, 1000),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CarBookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);

        CarAudit.Record(db, CarAuditEntity.Booking, booking.CarBookingId, "BookingCreated", "Booking started — waiting for payment",
            CarActor.Customer(customerId), newValue: $"Estimated {Money(fare.Total)} for {request.EstimatedKm} km", carBookingId: booking.CarBookingId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetForCustomerAsync(customerId, booking.CarBookingId, cancellationToken);
    }

    public async Task<IReadOnlyList<CarBookingDto>> ListForCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var bookings = await WithDetails()
            .Where(b => b.CustomerId == customerId && b.Status != CarBookingStatus.Expired)
            .OrderByDescending(b => b.PickupAt)
            .ToListAsync(cancellationToken);
        var timelines = await TimelinesAsync(bookings.Select(b => b.CarBookingId).ToList(), customerView: true, cancellationToken);
        return bookings.Select(b => ToDto(b, timelines.GetValueOrDefault(b.CarBookingId, []), CarActorView.Customer)).ToList();
    }

    public async Task<CarBookingDto> GetForCustomerAsync(int customerId, int carBookingId, CancellationToken cancellationToken = default)
    {
        var booking = await WithDetails().FirstOrDefaultAsync(b => b.CarBookingId == carBookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        var timelines = await TimelinesAsync([carBookingId], customerView: true, cancellationToken);
        return ToDto(booking, timelines.GetValueOrDefault(carBookingId, []), CarActorView.Customer);
    }

    public async Task<CarBookingDto> CancelByCustomerAsync(int customerId, int carBookingId, string? reason, CancellationToken cancellationToken = default)
    {
        await CancelAsync(carBookingId, b => b.CustomerId == customerId, CarActor.Customer(customerId), Clean(reason, 500) ?? "Cancelled by customer", waiveRefund: false,
            b =>
            {
                if (b.Status == CarBookingStatus.Confirmed && b.PickupAt <= DateTime.UtcNow)
                {
                    throw new ConflictException("The pickup time has passed. Please contact Ghumo Odisha to cancel.");
                }
            }, cancellationToken);
        return await GetForCustomerAsync(customerId, carBookingId, cancellationToken);
    }

    // =============== Driver ===============

    public async Task<CarBookingDto> CancelByDriverAsync(int driverId, int carBookingId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = Clean(reason, 500) ?? throw new ValidationAppException(["Please tell the customer why you're cancelling."]);
        await CancelAsync(carBookingId, b => b.DriverId == driverId, CarActor.Driver(driverId), why, waiveRefund: false,
            b =>
            {
                if (b.Status != CarBookingStatus.Confirmed)
                {
                    throw new ConflictException("Only a confirmed booking that hasn't started can be cancelled.");
                }
            }, cancellationToken);

        var booking = await WithDetails().FirstAsync(b => b.CarBookingId == carBookingId, cancellationToken);
        var timelines = await TimelinesAsync([carBookingId], customerView: true, cancellationToken);
        return ToDto(booking, timelines.GetValueOrDefault(carBookingId, []), CarActorView.Driver);
    }

    // =============== Admin ===============

    public async Task<PagedResult<CarBookingSummaryDto>> ListAsync(CarBookingStatus? status, CarPaymentStatus? paymentStatus, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.CarBookings.AsNoTracking();
        query = status is null
            ? query.Where(b => b.Status != CarBookingStatus.Expired && b.Status != CarBookingStatus.PendingPayment)
            : query.Where(b => b.Status == status);
        if (paymentStatus is not null) query = query.Where(b => b.PaymentStatus == paymentStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var number = int.TryParse(term.Replace("GC-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : (int?)null;
            var plate = CarRules.NormalizeRegistration(term);
            query = query.Where(b => b.BookingNumber == number || b.Customer.Name.Contains(term)
                || (b.Customer.PhoneNumber != null && b.Customer.PhoneNumber.Contains(term))
                || b.Driver.Name.Contains(term) || (plate != "" && b.Car.RegistrationNumber.Contains(plate)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(b => b.PickupAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.CarBookingId, b.BookingNumber, CustomerName = b.Customer.Name, b.Car.Brand, b.Car.ModelName, DriverName = b.Driver.Name,
                b.PickupAt, b.Status, b.PaymentStatus, b.EstimatedTotal, b.FinalTotal, b.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CarBookingSummaryDto>
        {
            Items = rows.Select(r => new CarBookingSummaryDto(r.CarBookingId, $"GC-{r.BookingNumber}", r.CustomerName,
                CarRules.DisplayName(r.Brand, r.ModelName), r.DriverName, r.PickupAt, r.Status, r.PaymentStatus, r.EstimatedTotal, r.FinalTotal, r.CreatedAt)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminCarBookingDetailDto> GetForAdminAsync(int carBookingId, CancellationToken cancellationToken = default)
    {
        var booking = await WithDetails().Include(b => b.Customer).FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        var timelines = await TimelinesAsync([carBookingId], customerView: true, cancellationToken);
        var history = await db.CarAuditEvents.AsNoTracking()
            .Where(e => e.CarBookingId == carBookingId)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.CarAuditEventId)
            .Select(e => new CarAuditEventDto(e.CarAuditEventId, e.EntityType, e.Action, e.Title, e.OldValue, e.NewValue, e.Note, e.ActorRole, e.ActorId, e.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminCarBookingDetailDto(
            ToDto(booking, timelines.GetValueOrDefault(carBookingId, []), CarActorView.Admin),
            new AdminCarBookingCustomerDto(booking.Customer.CustomerId, booking.Customer.Name, booking.Customer.PhoneNumber, booking.Customer.Email),
            booking.AdminNotes,
            booking.RazorpayOrderId,
            booking.RazorpayPaymentId,
            booking.RazorpayRefundId,
            history);
    }

    public async Task<AdminCarBookingDetailDto> CancelByAdminAsync(int adminId, int carBookingId, AdminCancelCarBookingRequest request, CancellationToken cancellationToken = default)
    {
        var why = Clean(request.Reason, 500) ?? throw new ValidationAppException(["Please give a reason — the customer will see it."]);
        await CancelAsync(carBookingId, _ => true, CarActor.Admin(adminId), why, request.WaiveRefund, _ => { }, cancellationToken);
        return await GetForAdminAsync(carBookingId, cancellationToken);
    }

    public async Task<AdminCarBookingDetailDto> UpdateNotesAsync(int carBookingId, string? notes, CancellationToken cancellationToken = default)
    {
        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        booking.AdminNotes = Clean(notes, 4000);
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetForAdminAsync(carBookingId, cancellationToken);
    }

    public async Task<AdminCarBookingDetailDto> IssueRazorpayRefundAsync(int adminId, int carBookingId, decimal amount, CancellationToken cancellationToken = default)
    {
        var booking = await LoadRefundPendingAsync(carBookingId, cancellationToken);
        if (string.IsNullOrWhiteSpace(booking.RazorpayPaymentId))
        {
            throw new ConflictException("This booking wasn't paid through Razorpay — record a manual refund instead.");
        }
        ValidateRefundAmount(amount, booking.BookingAmount);

        await ClaimRefundAsync(carBookingId, cancellationToken);
        string gatewayRefundId;
        try
        {
            gatewayRefundId = await razorpay.RefundAsync(booking.RazorpayPaymentId, (long)Math.Round(amount * 100m), cancellationToken);
        }
        catch
        {
            // Hand the request back to the queue so it can be retried or refunded manually.
            await db.CarBookings.Where(b => b.CarBookingId == carBookingId && b.PaymentStatus == CarPaymentStatus.RefundProcessing)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.PaymentStatus, CarPaymentStatus.RefundPending), cancellationToken);
            throw;
        }

        await RecordRefundIssuedAsync(adminId, carBookingId, amount, PaymentMethod.Razorpay, gatewayRefundId, gatewayRefundId, cancellationToken);
        return await GetForAdminAsync(carBookingId, cancellationToken);
    }

    public async Task<AdminCarBookingDetailDto> RecordManualRefundAsync(int adminId, int carBookingId, RecordCarManualRefundRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await LoadRefundPendingAsync(carBookingId, cancellationToken);
        if (request.Method == PaymentMethod.Razorpay)
        {
            throw new ValidationAppException(["Use \"Refund via Razorpay\" for an online refund."]);
        }
        ValidateRefundAmount(request.Amount, booking.BookingAmount);

        await ClaimRefundAsync(carBookingId, cancellationToken);
        await RecordRefundIssuedAsync(adminId, carBookingId, request.Amount, request.Method, Clean(request.Reference, 120), null, cancellationToken);
        return await GetForAdminAsync(carBookingId, cancellationToken);
    }

    public async Task<AdminCarBookingDetailDto> SettleRefundAsync(int adminId, int carBookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        if (booking.PaymentStatus != CarPaymentStatus.RefundProcessing)
        {
            throw new ConflictException("Only an issued refund can be marked as received.");
        }

        booking.PaymentStatus = CarPaymentStatus.Refunded;
        booking.RefundSettledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "RefundSettled", $"{Money(booking.RefundAmount ?? 0)} refunded",
            CarActor.Admin(adminId), nameof(CarPaymentStatus.RefundProcessing), nameof(CarPaymentStatus.Refunded), carBookingId: carBookingId, visibleToCustomer: true);
        await db.SaveChangesAsync(cancellationToken);
        return await GetForAdminAsync(carBookingId, cancellationToken);
    }

    // =============== System ===============

    public async Task<int> ExpireStaleHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await db.CarBookings
            .Where(b => b.Status == CarBookingStatus.PendingPayment && b.HoldExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, CarBookingStatus.Expired)
                .SetProperty(b => b.HoldExpiresAt, (DateTime?)null)
                .SetProperty(b => b.UpdatedAt, now), cancellationToken);
    }

    // =============== Confirmation (used by CarBookingPaymentService) ===============

    /// <summary>
    /// Confirms a paid booking — under the car lock, after re-checking nobody else took the car while
    /// the customer was paying. Returns false (booking cancelled by System, caller refunds) on a clash.
    /// The caller owns the transaction.
    /// </summary>
    internal async Task<bool> TryConfirmPaidAsync(CarBooking booking, string? razorpayPaymentId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await LockCarAsync(booking.CarId, cancellationToken);

        var clash = await db.CarBookings
            .Where(b => b.CarId == booking.CarId && b.CarBookingId != booking.CarBookingId)
            .Overlapping(booking.PickupAt, booking.EndsAt, _options.TurnaroundBufferMinutes, now)
            .AnyAsync(cancellationToken);
        var stillListed = await db.Cars.Listed().AnyAsync(c => c.CarId == booking.CarId, cancellationToken);

        booking.RazorpayPaymentId = razorpayPaymentId;
        booking.PaidAt = now;
        booking.HoldExpiresAt = null;
        booking.UpdatedAt = now;

        if (clash || !stillListed)
        {
            booking.Status = CarBookingStatus.Cancelled;
            // The payment service refunds straight away and then marks it Refunded; if that fails it stays in the admin's queue.
            booking.PaymentStatus = CarPaymentStatus.RefundPending;
            booking.RefundAmount = booking.BookingAmount;
            booking.CancelledAt = now;
            booking.CancelledBy = "System";
            booking.CancellationReason = clash
                ? "The car was booked by someone else while you were paying."
                : "The car stopped taking bookings while you were paying.";
            CarAudit.Record(db, CarAuditEntity.Booking, booking.CarBookingId, "BookingUnconfirmable", "Car no longer available — payment refunded",
                CarActor.System, note: booking.CancellationReason, carBookingId: booking.CarBookingId, visibleToCustomer: true);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        booking.Status = CarBookingStatus.Confirmed;
        booking.PaymentStatus = CarPaymentStatus.BookingAmountPaid;
        booking.ConfirmedAt = now;
        var driverName = await db.Drivers.Where(d => d.DriverId == booking.DriverId).Select(d => d.Name).FirstAsync(cancellationToken);
        CarAudit.Record(db, CarAuditEntity.Booking, booking.CarBookingId, "BookingConfirmed", $"Booking confirmed — {Money(booking.BookingAmount)} paid",
            CarActor.Customer(booking.CustomerId), nameof(CarBookingStatus.PendingPayment), nameof(CarBookingStatus.Confirmed),
            carBookingId: booking.CarBookingId, visibleToCustomer: true);
        CarAudit.Record(db, CarAuditEntity.Booking, booking.CarBookingId, "DriverAssigned", $"Driver assigned: {driverName}",
            CarActor.System, newValue: booking.DriverId.ToString(CultureInfo.InvariantCulture), carBookingId: booking.CarBookingId, visibleToCustomer: true);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Loads one booking as <paramref name="view"/> sees it (with the customer-visible timeline). No ownership check — callers do that.</summary>
    internal async Task<CarBookingDto> GetViewAsync(int carBookingId, CarActorView view, CancellationToken cancellationToken)
    {
        var booking = await WithDetails().FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        var timelines = await TimelinesAsync([carBookingId], customerView: true, cancellationToken);
        return ToDto(booking, timelines.GetValueOrDefault(carBookingId, []), view);
    }

    // =============== helpers ===============

    /// <summary>
    /// The single cancel path for customers, drivers and admins. Locks the booking row so a cancel can't
    /// race a Start Trip or a payment confirmation.
    /// </summary>
    private async Task CancelAsync(int carBookingId, Func<CarBooking, bool> ownedBy, CarActor actor, string reason, bool waiveRefund,
        Action<CarBooking> extraRules, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarBookingId FROM CarBookings WHERE CarBookingId = {carBookingId} FOR UPDATE", cancellationToken);

        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken);
        if (booking is null || !ownedBy(booking))
        {
            throw new NotFoundException("Booking not found.");
        }

        switch (booking.Status)
        {
            case CarBookingStatus.Cancelled:
                throw new ConflictException("This booking is already cancelled.");
            case CarBookingStatus.InProgress:
                throw new ConflictException("The trip has already started, so it can't be cancelled.");
            case CarBookingStatus.Completed:
                throw new ConflictException("This trip is already completed.");
        }
        extraRules(booking);

        var now = DateTime.UtcNow;
        var oldStatus = booking.Status;
        var paid = booking.PaymentStatus == CarPaymentStatus.BookingAmountPaid;

        booking.Status = CarBookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancelledBy = actor.Role;
        booking.CancellationReason = reason;
        booking.HoldExpiresAt = null;
        booking.UpdatedAt = now;
        if (paid && !waiveRefund)
        {
            booking.PaymentStatus = CarPaymentStatus.RefundPending;
            booking.RefundAmount = booking.BookingAmount;
        }

        var who = actor.Role switch { "Customer" => "you", "Driver" => "the driver", "Admin" => "Ghumo Odisha", _ => "the system" };
        CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "BookingCancelled", $"Booking cancelled by {who}", actor,
            oldStatus.ToString(), nameof(CarBookingStatus.Cancelled), reason, carBookingId, visibleToCustomer: true);
        if (paid && !waiveRefund)
        {
            CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "RefundRequested", $"Refund of {Money(booking.BookingAmount)} requested",
                actor, carBookingId: carBookingId, visibleToCustomer: true);
        }
        else if (paid)
        {
            CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "RefundWaived", "Booking amount kept (no refund)", actor, note: reason,
                carBookingId: carBookingId, visibleToCustomer: false);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Car booking {CarBookingId} cancelled by {Role} {ActorId}.", carBookingId, actor.Role, actor.Id);
    }

    internal async Task LockCarAsync(int carId, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarId FROM Cars WHERE CarId = {carId} FOR UPDATE", cancellationToken);

    private async Task<CarBooking> LoadRefundPendingAsync(int carBookingId, CancellationToken cancellationToken)
    {
        var booking = await db.CarBookings.AsNoTracking().FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        if (booking.PaymentStatus != CarPaymentStatus.RefundPending)
        {
            throw new ConflictException("There's no refund waiting to be issued for this booking.");
        }
        return booking;
    }

    /// <summary>Moves the refund off Pending with a conditional update — only one caller can, so a double click never refunds twice.</summary>
    private async Task ClaimRefundAsync(int carBookingId, CancellationToken cancellationToken)
    {
        var claimed = await db.CarBookings
            .Where(b => b.CarBookingId == carBookingId && b.PaymentStatus == CarPaymentStatus.RefundPending)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.PaymentStatus, CarPaymentStatus.RefundProcessing), cancellationToken);
        if (claimed != 1)
        {
            throw new ConflictException("This refund is already being processed.");
        }
    }

    private async Task RecordRefundIssuedAsync(int adminId, int carBookingId, decimal amount, PaymentMethod method, string? reference, string? razorpayRefundId, CancellationToken cancellationToken)
    {
        var booking = await db.CarBookings.FirstAsync(b => b.CarBookingId == carBookingId, cancellationToken);
        // Already set in the database by the claim; set here too so a tracked copy of the booking agrees.
        booking.PaymentStatus = CarPaymentStatus.RefundProcessing;
        booking.RefundAmount = amount;
        booking.RefundMethod = method;
        booking.RefundReference = reference;
        booking.RazorpayRefundId = razorpayRefundId;
        booking.RefundIssuedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "RefundIssued", $"Refund of {Money(amount)} initiated", CarActor.Admin(adminId),
            nameof(CarPaymentStatus.RefundPending), nameof(CarPaymentStatus.RefundProcessing), $"{method} {reference}".Trim(), carBookingId, visibleToCustomer: true);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRefundAmount(decimal amount, decimal paid)
    {
        if (amount <= 0 || amount > paid)
        {
            throw new ValidationAppException([$"Refund must be more than ₹0 and at most {Money(paid)}."]);
        }
    }

    private IQueryable<CarBooking> WithDetails() =>
        db.CarBookings.AsNoTracking()
            .Include(b => b.Car).ThenInclude(c => c.Photos.Where(p => p.Kind == CarPhotoKind.Exterior))
            .Include(b => b.Driver)
            .Include(b => b.CarPricing)
            .Include(b => b.Execution)
            .AsSplitQuery();

    private async Task<Dictionary<int, List<CarTimelineEventDto>>> TimelinesAsync(List<int> bookingIds, bool customerView, CancellationToken cancellationToken)
    {
        var events = await db.CarAuditEvents.AsNoTracking()
            .Where(e => e.CarBookingId != null && bookingIds.Contains(e.CarBookingId.Value) && (!customerView || e.IsVisibleToCustomer))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.CarAuditEventId)
            .Select(e => new { BookingId = e.CarBookingId!.Value, Dto = new CarTimelineEventDto(e.Title, e.Note, e.ActorRole, e.CreatedAt) })
            .ToListAsync(cancellationToken);
        return events.GroupBy(e => e.BookingId).ToDictionary(g => g.Key, g => g.Select(e => e.Dto).ToList());
    }

    internal enum CarActorView { Customer, Driver, Admin }

    internal static CarBookingDto ToDto(CarBooking b, List<CarTimelineEventDto> timeline, CarActorView view)
    {
        var now = DateTime.UtcNow;
        var confirmed = b.Status is CarBookingStatus.Confirmed or CarBookingStatus.InProgress or CarBookingStatus.Completed;
        var estimate = new FareBreakdownDto(b.EstimatedKm, b.EstimatedBaseFare, b.EstimatedKmCharge,
            b.EstimatedNights, b.EstimatedNightHaltCharge, 0m, null, b.EstimatedTotal);
        var final = b.FinalTotal is null
            ? null
            : new FareBreakdownDto(b.FinalKm ?? 0, b.FinalBaseFare ?? 0, b.FinalKmCharge ?? 0,
                b.FinalNights ?? 0, b.FinalNightHaltCharge ?? 0, b.AdditionalCharges ?? 0, b.AdditionalChargesNote, b.FinalTotal.Value);
        var refund = b.PaymentStatus is CarPaymentStatus.RefundPending or CarPaymentStatus.RefundProcessing or CarPaymentStatus.Refunded
            ? new CarRefundDto(b.RefundAmount ?? b.BookingAmount, b.PaymentStatus, b.RefundMethod, view == CarActorView.Driver ? null : b.RefundReference,
                b.RefundIssuedAt, b.RefundSettledAt)
            : null;
        var trip = b.Execution is null
            ? null
            : new CarTripDto(b.Execution.StartedAt, b.Execution.StartOdometerKm, b.Execution.EndedAt, b.Execution.EndOdometerKm,
                b.Execution.ActualKm, b.Execution.NightHalts, b.Execution.CompletedAt);
        var paidTowardsFare = b.PaymentStatus is CarPaymentStatus.BookingAmountPaid or CarPaymentStatus.BalanceCollected ? b.BookingAmount : 0m;

        return new CarBookingDto(
            b.CarBookingId,
            b.Reference,
            b.Status,
            b.PaymentStatus,
            b.CarId,
            CarRules.DisplayName(b.Car.Brand, b.Car.ModelName),
            CarRules.Category(b.Car.SeatCapacity),
            b.Car.FuelType,
            b.Car.SeatCapacity,
            b.Car.HasAc,
            b.Car.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).FirstOrDefault(),
            confirmed || view != CarActorView.Customer ? b.Car.RegistrationNumber : null,
            confirmed || view == CarActorView.Admin
                ? new AssignedDriverDto(b.DriverId, b.Driver.Name, b.Driver.PhoneNumber, b.Driver.ProfilePhotoUrl)
                : null,
            b.PickupCity,
            b.PickupAddress,
            b.PickupAt,
            b.DurationHours,
            b.EndsAt,
            b.CarPricing.PricePerKm,
            b.CarPricing.NightHaltPrice,
            estimate,
            final,
            b.BookingAmount,
            Math.Max((final?.Total ?? estimate.Total) - paidTowardsFare, 0m),
            b.BalanceCollectedAt,
            trip,
            refund,
            b.Status == CarBookingStatus.PendingPayment ? b.HoldExpiresAt : null,
            b.CustomerNotes,
            b.CancelledBy,
            b.CancellationReason,
            b.ConfirmedAt,
            b.CancelledAt,
            b.CreatedAt,
            view == CarActorView.Customer && b.Status is CarBookingStatus.PendingPayment or CarBookingStatus.Expired && b.PickupAt > now,
            view switch
            {
                CarActorView.Customer => b.Status is CarBookingStatus.PendingPayment or CarBookingStatus.Expired
                    || (b.Status == CarBookingStatus.Confirmed && b.PickupAt > now),
                CarActorView.Driver => b.Status == CarBookingStatus.Confirmed,
                _ => b.Status is not (CarBookingStatus.Cancelled or CarBookingStatus.Completed or CarBookingStatus.InProgress)
            },
            timeline);
    }

    internal static string Money(decimal amount) => "₹" + amount.ToString("#,##0.##", CultureInfo.GetCultureInfo("en-IN"));

    private static string? Clean(string? text, int max)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
