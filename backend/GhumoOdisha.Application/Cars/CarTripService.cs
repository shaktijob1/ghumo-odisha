using System.Data;
using System.Globalization;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static GhumoOdisha.Application.Cars.CarBookingService;

namespace GhumoOdisha.Application.Cars;

public interface ICarTripService
{
    Task<IReadOnlyList<DriverBookingDto>> ListForDriverAsync(int driverId, DriverBookingScope scope, CancellationToken cancellationToken = default);
    Task<DriverBookingDto> GetForDriverAsync(int driverId, int carBookingId, CancellationToken cancellationToken = default);
    Task<DriverBookingDto> StartTripAsync(int driverId, int carBookingId, StartTripRequest request, CancellationToken cancellationToken = default);
    Task<TripFarePreviewDto> PreviewEndAsync(int driverId, int carBookingId, EndTripRequest request, CancellationToken cancellationToken = default);
    Task<DriverBookingDto> CompleteTripAsync(int driverId, int carBookingId, EndTripRequest request, CancellationToken cancellationToken = default);
    Task<DriverBookingDto> MarkBalanceCollectedAsync(int driverId, int carBookingId, CancellationToken cancellationToken = default);
    Task<DriverEarningsDto> GetEarningsAsync(int driverId, CancellationToken cancellationToken = default);

    Task<AdminCarBookingDetailDto> AdminCorrectFareAsync(int adminId, int carBookingId, AdminCorrectFareRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What happens on the road. The assigned driver starts the trip (odometer + time + optional GPS),
/// ends it (odometer, night halts, tolls/parking), sees the server-calculated final fare and completes
/// it. Actual km = end − start odometer; the estimate on the booking is never overwritten. Once
/// completed, the driver can't change anything — only an admin correction can, and it's audited.
/// Every write locks the booking row, so Start / Complete / Cancel can't interleave.
/// </summary>
public class CarTripService(
    IGhumoOdishaDbContext db,
    CarBookingService bookingService,
    IOptions<CarRentalOptions> options) : ICarTripService
{
    private const int MaxOdometerKm = 9_999_999;
    private const decimal MaxAdditionalCharges = 50_000m;
    /// <summary>A driver can press Start Trip up to this long before the booked pickup time.</summary>
    private static readonly TimeSpan EarliestStartBeforePickup = TimeSpan.FromHours(2);

    private readonly CarRentalOptions _options = options.Value;

    // ---------------- Driver: bookings ----------------

    public async Task<IReadOnlyList<DriverBookingDto>> ListForDriverAsync(int driverId, DriverBookingScope scope, CancellationToken cancellationToken = default)
    {
        var query = db.CarBookings.AsNoTracking().Where(b => b.DriverId == driverId);
        query = scope switch
        {
            DriverBookingScope.Upcoming => query.Where(b => b.Status == CarBookingStatus.Confirmed).OrderBy(b => b.PickupAt),
            DriverBookingScope.Active => query.Where(b => b.Status == CarBookingStatus.InProgress).OrderBy(b => b.PickupAt),
            _ => query.Where(b => b.Status == CarBookingStatus.Completed || (b.Status == CarBookingStatus.Cancelled && b.ConfirmedAt != null))
                .OrderByDescending(b => b.PickupAt).Take(100)
        };

        var ids = await query.Select(b => b.CarBookingId).ToListAsync(cancellationToken);
        var result = new List<DriverBookingDto>(ids.Count);
        foreach (var id in ids)
        {
            result.Add(await BuildDriverViewAsync(id, cancellationToken));
        }
        return result;
    }

    public async Task<DriverBookingDto> GetForDriverAsync(int driverId, int carBookingId, CancellationToken cancellationToken = default)
    {
        await EnsureDriverOwnsPaidBookingAsync(driverId, carBookingId, cancellationToken);
        return await BuildDriverViewAsync(carBookingId, cancellationToken);
    }

    // ---------------- Driver: start / end ----------------

    public async Task<DriverBookingDto> StartTripAsync(int driverId, int carBookingId, StartTripRequest request, CancellationToken cancellationToken = default)
    {
        ValidateOdometer(request.StartOdometerKm, "Start KM");
        ValidateLocation(request.Latitude, request.Longitude);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var booking = await LockOwnBookingAsync(driverId, carBookingId, cancellationToken);

        switch (booking.Status)
        {
            case CarBookingStatus.InProgress:
                throw new ConflictException("This trip has already started.");
            case CarBookingStatus.Completed:
                throw new ConflictException("This trip is already completed.");
            case CarBookingStatus.Cancelled:
                throw new ConflictException("This booking was cancelled.");
            case not CarBookingStatus.Confirmed:
                throw new NotFoundException("Booking not found.");
        }

        var driverStatus = await db.Drivers.Where(d => d.DriverId == driverId).Select(d => d.Status).FirstAsync(cancellationToken);
        if (driverStatus == DriverStatus.Suspended)
        {
            throw new ConflictException("Your account is suspended. Please contact Ghumo Odisha.");
        }

        var now = DateTime.UtcNow;
        var earliest = booking.PickupAt - EarliestStartBeforePickup;
        if (now < earliest)
        {
            throw new ConflictException($"You can start this trip from {IndiaTime(earliest)}.");
        }

        // The odometer never goes backwards: the last completed trip in this car ended at a higher reading.
        var lastEnd = await LastEndOdometerAsync(booking.CarId, carBookingId, cancellationToken);
        if (lastEnd is not null && request.StartOdometerKm < lastEnd)
        {
            throw new ValidationAppException([$"Start KM can't be lower than this car's last trip end reading ({lastEnd:N0} km). Please check the odometer."]);
        }

        db.CarTripExecutions.Add(new CarTripExecution
        {
            CarBookingId = carBookingId,
            DriverId = driverId,
            CarId = booking.CarId,
            StartedAt = now,
            StartOdometerKm = request.StartOdometerKm,
            StartLatitude = request.Latitude,
            StartLongitude = request.Longitude
        });
        booking.Status = CarBookingStatus.InProgress;
        booking.UpdatedAt = now;
        CarAudit.Record(db, CarAuditEntity.Trip, carBookingId, "TripStarted", "Trip started", CarActor.Driver(driverId),
            nameof(CarBookingStatus.Confirmed), nameof(CarBookingStatus.InProgress), $"Start KM {request.StartOdometerKm:N0}",
            carBookingId, visibleToCustomer: true);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildDriverViewAsync(carBookingId, cancellationToken);
    }

    public async Task<TripFarePreviewDto> PreviewEndAsync(int driverId, int carBookingId, EndTripRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await LoadForFareAsync(carBookingId, cancellationToken);
        if (booking.DriverId != driverId)
        {
            throw new NotFoundException("Booking not found.");
        }
        EnsureRunning(booking);
        return BuildPreview(booking, request, DateTime.UtcNow);
    }

    public async Task<DriverBookingDto> CompleteTripAsync(int driverId, int carBookingId, EndTripRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockOwnBookingAsync(driverId, carBookingId, cancellationToken);

        var booking = await LoadForFareAsync(carBookingId, cancellationToken, tracking: true);
        EnsureRunning(booking);

        var now = DateTime.UtcNow;
        var preview = BuildPreview(booking, request, now);
        var execution = booking.Execution!;
        execution.EndedAt = now;
        execution.EndOdometerKm = request.EndOdometerKm;
        execution.EndLatitude = request.Latitude;
        execution.EndLongitude = request.Longitude;
        execution.ActualKm = preview.ActualKm;
        execution.NightHalts = request.NightHalts;
        execution.CompletedAt = now;

        ApplyFinal(booking, preview.Final, preview.BalanceDue);
        booking.Status = CarBookingStatus.Completed;
        booking.CompletedAt = now;
        booking.UpdatedAt = now;

        CarAudit.Record(db, CarAuditEntity.Trip, carBookingId, "TripCompleted", $"Trip completed — {preview.ActualKm:N0} km", CarActor.Driver(driverId),
            nameof(CarBookingStatus.InProgress), nameof(CarBookingStatus.Completed), $"End KM {request.EndOdometerKm:N0}", carBookingId, visibleToCustomer: true);
        CarAudit.Record(db, CarAuditEntity.Trip, carBookingId, "FinalFareSet", $"Final fare {Money(preview.Final.Total)}", CarActor.Driver(driverId),
            $"Estimate {Money(booking.EstimatedTotal)}", Describe(preview.Final), carBookingId: carBookingId, visibleToCustomer: true);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildDriverViewAsync(carBookingId, cancellationToken);
    }

    public async Task<DriverBookingDto> MarkBalanceCollectedAsync(int driverId, int carBookingId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var booking = await LockOwnBookingAsync(driverId, carBookingId, cancellationToken);
        if (booking.Status != CarBookingStatus.Completed)
        {
            throw new ConflictException("Complete the trip first.");
        }
        if (booking.PaymentStatus == CarPaymentStatus.BalanceCollected)
        {
            throw new ConflictException("The balance is already marked as collected.");
        }

        var now = DateTime.UtcNow;
        var old = booking.PaymentStatus;
        booking.PaymentStatus = CarPaymentStatus.BalanceCollected;
        booking.BalanceCollectedAt = now;
        booking.UpdatedAt = now;
        CarAudit.Record(db, CarAuditEntity.Booking, carBookingId, "BalanceCollected", $"Balance of {Money(booking.BalanceDue ?? 0)} paid to driver",
            CarActor.Driver(driverId), old.ToString(), nameof(CarPaymentStatus.BalanceCollected), carBookingId: carBookingId, visibleToCustomer: true);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildDriverViewAsync(carBookingId, cancellationToken);
    }

    public async Task<DriverEarningsDto> GetEarningsAsync(int driverId, CancellationToken cancellationToken = default)
    {
        var mine = db.CarBookings.AsNoTracking().Where(b => b.DriverId == driverId);
        var completed = mine.Where(b => b.Status == CarBookingStatus.Completed);

        var upcoming = await mine.CountAsync(b => b.Status == CarBookingStatus.Confirmed || b.Status == CarBookingStatus.InProgress, cancellationToken);
        var completedCount = await completed.CountAsync(cancellationToken);
        var totalKm = await completed.SumAsync(b => (int?)b.FinalKm, cancellationToken) ?? 0;
        var totalFare = await completed.SumAsync(b => b.FinalTotal, cancellationToken) ?? 0m;
        var online = await completed.SumAsync(b => (decimal?)b.BookingAmount, cancellationToken) ?? 0m;
        var collected = await completed.Where(b => b.PaymentStatus == CarPaymentStatus.BalanceCollected).SumAsync(b => b.BalanceDue, cancellationToken) ?? 0m;
        var pending = await completed.Where(b => b.PaymentStatus != CarPaymentStatus.BalanceCollected).SumAsync(b => b.BalanceDue, cancellationToken) ?? 0m;

        var recent = await completed.OrderByDescending(b => b.CompletedAt).Take(10)
            .Select(b => new
            {
                b.CarBookingId, b.BookingNumber, CustomerName = b.Customer.Name, b.Car.Brand, b.Car.ModelName, DriverName = b.Driver.Name,
                b.PickupAt, b.Status, b.PaymentStatus, b.EstimatedTotal, b.FinalTotal, b.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new DriverEarningsDto(upcoming, completedCount, totalKm, totalFare, online, collected, pending,
            recent.Select(r => new CarBookingSummaryDto(r.CarBookingId, $"GC-{r.BookingNumber}", r.CustomerName, CarRules.DisplayName(r.Brand, r.ModelName),
                r.DriverName, r.PickupAt, r.Status, r.PaymentStatus, r.EstimatedTotal, r.FinalTotal, r.CreatedAt)).ToList());
    }

    // ---------------- Admin: correction ----------------

    public async Task<AdminCarBookingDetailDto> AdminCorrectFareAsync(int adminId, int carBookingId, AdminCorrectFareRequest request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new ValidationAppException(["Please give a reason for the correction — it's kept in the history and shown to the customer."]);
        }
        ValidateOdometer(request.StartOdometerKm, "Start KM");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarBookingId FROM CarBookings WHERE CarBookingId = {carBookingId} FOR UPDATE", cancellationToken);
        var booking = await LoadForFareAsync(carBookingId, cancellationToken, tracking: true);
        if (booking.Status != CarBookingStatus.Completed || booking.Execution is null)
        {
            throw new ConflictException("Only a completed trip's fare can be corrected.");
        }

        var execution = booking.Execution;
        var maxNights = CarRentalCalendar.NightsSpanned(execution.StartedAt, execution.EndedAt ?? execution.StartedAt);
        var final = ComputeFinal(booking, request.StartOdometerKm,
            new EndTripRequest(request.EndOdometerKm, request.NightHalts, request.AdditionalCharges, request.AdditionalChargesNote, null, null), maxNights);

        var before = $"{Describe(Final(booking))} · Odometer {execution.StartOdometerKm:N0}–{execution.EndOdometerKm:N0}";
        execution.StartOdometerKm = request.StartOdometerKm;
        execution.EndOdometerKm = request.EndOdometerKm;
        execution.ActualKm = final.Km;
        execution.NightHalts = request.NightHalts;

        var paid = booking.BookingAmount;
        ApplyFinal(booking, final, Math.Max(final.Total - paid, 0m));
        booking.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Trip, carBookingId, "FinalFareCorrected", $"Final fare corrected to {Money(final.Total)}", CarActor.Admin(adminId),
            before, $"{Describe(final)} · Odometer {request.StartOdometerKm:N0}–{request.EndOdometerKm:N0}", reason, carBookingId, visibleToCustomer: true);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await bookingService.GetForAdminAsync(carBookingId, cancellationToken);
    }

    // ---------------- helpers ----------------

    private TripFarePreviewDto BuildPreview(CarBooking booking, EndTripRequest request, DateTime nowUtc)
    {
        var execution = booking.Execution!;
        var maxNights = CarRentalCalendar.NightsSpanned(execution.StartedAt, nowUtc);
        var final = ComputeFinal(booking, execution.StartOdometerKm, request, maxNights);
        var estimate = new FareBreakdownDto(booking.EstimatedKm, booking.EstimatedBaseFare,
            booking.EstimatedKmCharge, booking.EstimatedNights, booking.EstimatedNightHaltCharge, 0m, null, booking.EstimatedTotal);
        var paid = booking.BookingAmount;
        return new TripFarePreviewDto(execution.StartOdometerKm, request.EndOdometerKm, final.Km, maxNights, estimate, final, paid, Math.Max(final.Total - paid, 0m));
    }

    /// <summary>Validates the end-of-trip inputs and prices them with the booking's own (frozen) pricing version.</summary>
    private FareBreakdownDto ComputeFinal(CarBooking booking, int startOdometerKm, EndTripRequest request, int maxNights)
    {
        var errors = new List<string>();
        if (request.EndOdometerKm < startOdometerKm)
        {
            errors.Add($"End KM can't be less than start KM ({startOdometerKm:N0}).");
        }
        else if (request.EndOdometerKm - startOdometerKm > _options.MaxTripKm)
        {
            errors.Add($"That's more than {_options.MaxTripKm:N0} km for one trip — please check the End KM.");
        }
        if (request.EndOdometerKm > MaxOdometerKm)
        {
            errors.Add("Enter a valid End KM.");
        }
        if (request.NightHalts < 0 || request.NightHalts > maxNights)
        {
            errors.Add(maxNights == 0
                ? "This trip hasn't gone past midnight, so there's no night halt."
                : $"Night halts can be at most {maxNights} for this trip.");
        }
        if (request.AdditionalCharges < 0 || request.AdditionalCharges > MaxAdditionalCharges)
        {
            errors.Add($"Extra charges must be between ₹0 and {Money(MaxAdditionalCharges)}.");
        }
        if (request.AdditionalCharges > 0 && string.IsNullOrWhiteSpace(request.AdditionalChargesNote))
        {
            errors.Add("Say what the extra charges are for (e.g. tolls, parking).");
        }
        ValidateLocation(request.Latitude, request.Longitude, errors);
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var actualKm = request.EndOdometerKm - startOdometerKm;
        var fare = CarFareCalculator.Calculate(FareTerms.From(booking.CarPricing), actualKm, request.NightHalts, request.AdditionalCharges);
        var note = request.AdditionalCharges > 0 ? request.AdditionalChargesNote!.Trim() : null;
        return new FareBreakdownDto(fare.Km, fare.BaseFare, fare.KmCharge, fare.Nights, fare.NightHaltCharge,
            fare.AdditionalCharges, note is { Length: > 300 } ? note[..300] : note, fare.Total);
    }

    private static void ApplyFinal(CarBooking booking, FareBreakdownDto final, decimal balanceDue)
    {
        booking.FinalKm = final.Km;
        booking.FinalNights = final.Nights;
        booking.FinalBaseFare = final.BaseFare;
        booking.FinalKmCharge = final.KmCharge;
        booking.FinalNightHaltCharge = final.NightHaltCharge;
        booking.AdditionalCharges = final.AdditionalCharges;
        booking.AdditionalChargesNote = final.AdditionalChargesNote;
        booking.FinalTotal = final.Total;
        booking.BalanceDue = balanceDue;
    }

    private static FareBreakdownDto Final(CarBooking b) =>
        new(b.FinalKm ?? 0, b.FinalBaseFare ?? 0, b.FinalKmCharge ?? 0, b.FinalNights ?? 0, b.FinalNightHaltCharge ?? 0,
            b.AdditionalCharges ?? 0, b.AdditionalChargesNote, b.FinalTotal ?? 0);

    private static string Describe(FareBreakdownDto f) =>
        $"{f.Km:N0} km · base {Money(f.BaseFare)} · km {Money(f.KmCharge)} · {f.Nights} night(s) {Money(f.NightHaltCharge)} · extras {Money(f.AdditionalCharges)} = {Money(f.Total)}";

    private static void EnsureRunning(CarBooking booking)
    {
        switch (booking.Status)
        {
            case CarBookingStatus.Completed:
                throw new ConflictException("This trip is already completed.");
            case CarBookingStatus.Confirmed:
                throw new ConflictException("Start the trip first.");
            case not CarBookingStatus.InProgress:
                throw new ConflictException("This trip isn't running.");
        }
    }

    /// <summary>Locks the driver's own booking row for the rest of the transaction. Unpaid bookings aren't the driver's yet.</summary>
    private async Task<CarBooking> LockOwnBookingAsync(int driverId, int carBookingId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarBookingId FROM CarBookings WHERE CarBookingId = {carBookingId} FOR UPDATE", cancellationToken);
        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId && b.DriverId == driverId
            && b.Status != CarBookingStatus.PendingPayment && b.Status != CarBookingStatus.Expired, cancellationToken);
        return booking ?? throw new NotFoundException("Booking not found.");
    }

    private async Task EnsureDriverOwnsPaidBookingAsync(int driverId, int carBookingId, CancellationToken cancellationToken)
    {
        var mine = await db.CarBookings.AnyAsync(b => b.CarBookingId == carBookingId && b.DriverId == driverId
            && b.Status != CarBookingStatus.PendingPayment && b.Status != CarBookingStatus.Expired, cancellationToken);
        if (!mine)
        {
            throw new NotFoundException("Booking not found.");
        }
    }

    private async Task<CarBooking> LoadForFareAsync(int carBookingId, CancellationToken cancellationToken, bool tracking = false)
    {
        var query = db.CarBookings.Include(b => b.CarPricing).ThenInclude(p => p.Tiers).Include(b => b.Execution).AsSplitQuery();
        return await (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(b => b.CarBookingId == carBookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
    }

    private Task<int?> LastEndOdometerAsync(int carId, int exceptBookingId, CancellationToken cancellationToken) =>
        db.CarTripExecutions.AsNoTracking()
            .Where(e => e.CarId == carId && e.CarBookingId != exceptBookingId && e.EndOdometerKm != null)
            .OrderByDescending(e => e.EndedAt)
            .Select(e => e.EndOdometerKm)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<DriverBookingDto> BuildDriverViewAsync(int carBookingId, CancellationToken cancellationToken)
    {
        var view = await bookingService.GetViewAsync(carBookingId, CarActorView.Driver, cancellationToken);
        var customer = await db.CarBookings.AsNoTracking().Where(b => b.CarBookingId == carBookingId)
            .Select(b => new { b.Customer.Name, b.Customer.PhoneNumber }).FirstAsync(cancellationToken);
        // The customer's number is for reaching them about this trip — not kept on show afterwards.
        var showPhone = view.Status is CarBookingStatus.Confirmed or CarBookingStatus.InProgress;
        var lastEnd = view.Status == CarBookingStatus.Confirmed ? await LastEndOdometerAsync(view.CarId, carBookingId, cancellationToken) : null;
        return new DriverBookingDto(view, customer.Name, showPhone ? customer.PhoneNumber : null, lastEnd);
    }

    private static void ValidateOdometer(int km, string label)
    {
        if (km < 0 || km > MaxOdometerKm)
        {
            throw new ValidationAppException([$"Enter a valid {label}."]);
        }
    }

    private static void ValidateLocation(decimal? latitude, decimal? longitude, List<string>? errors = null)
    {
        var bad = (latitude is null) != (longitude is null)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180;
        if (!bad)
        {
            return;
        }
        if (errors is null)
        {
            throw new ValidationAppException(["The location reading looks wrong. Try again, or continue without location."]);
        }
        errors.Add("The location reading looks wrong. Try again, or continue without location.");
    }

    private static string IndiaTime(DateTime utc) =>
        CarRentalCalendar.UtcToIndia(utc).ToString("d MMM, h:mm tt", CultureInfo.GetCultureInfo("en-IN"));
}
