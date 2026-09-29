using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Cars;

public interface IAdminCarService
{
    Task<PagedResult<AdminDriverListItemDto>> ListDriversAsync(AdminCarFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailDto> GetDriverAsync(int driverId, CancellationToken cancellationToken = default);
    /// <summary>Adds an owner-driver on their behalf (e.g. signed up in person). They sign in later with this WhatsApp number.</summary>
    Task<AdminDriverDetailDto> CreateDriverAsync(int adminId, AdminCreateDriverRequest request, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailDto> ApproveDriverAsync(int adminId, int driverId, string? note, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailDto> RejectDriverAsync(int adminId, int driverId, string? reason, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailDto> SuspendDriverAsync(int adminId, int driverId, string? reason, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminCarListItemDto>> ListCarsAsync(AdminCarFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> GetCarAsync(int carId, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> ApproveCarAsync(int adminId, int carId, string? note, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> RejectCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> SuspendCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> DeactivateCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminPendingPricingDto>> ListPendingPricingAsync(CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> ApprovePricingAsync(int adminId, int pricingId, CancellationToken cancellationToken = default);
    Task<AdminCarDetailDto> RejectPricingAsync(int adminId, int pricingId, string? reason, CancellationToken cancellationToken = default);
    /// <summary>Admin edits pricing directly: a new version, approved and active at once (history kept).</summary>
    Task<AdminCarDetailDto> SetPricingAsync(int adminId, int carId, SubmitPricingRequest request, CancellationToken cancellationToken = default);

    Task<CarAdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin review of drivers, cars and pricing — the only place any of them becomes Approved. Every
/// decision is recorded in CarAuditEvents with the before/after value and the admin's id (and, like
/// every admin call, in AdminActivities by the AdminActivityFilter).
/// </summary>
public class AdminCarService(IGhumoOdishaDbContext db) : IAdminCarService
{
    // ---------------- Drivers ----------------

    public async Task<PagedResult<AdminDriverListItemDto>> ListDriversAsync(AdminCarFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        var query = db.Drivers.AsNoTracking();
        query = filter switch
        {
            AdminCarFilter.Review => query.Where(d => d.Status == DriverStatus.Pending && d.SubmittedForReviewAt != null),
            AdminCarFilter.Draft => query.Where(d => d.Status == DriverStatus.Pending && d.SubmittedForReviewAt == null),
            AdminCarFilter.Approved => query.Where(d => d.Status == DriverStatus.Approved),
            AdminCarFilter.Rejected => query.Where(d => d.Status == DriverStatus.Rejected),
            AdminCarFilter.Suspended => query.Where(d => d.Status == DriverStatus.Suspended),
            AdminCarFilter.Inactive => query.Where(_ => false),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.Name.Contains(term) || (d.PhoneNumber != null && d.PhoneNumber.Contains(term))
                || (d.DrivingLicenceNumber != null && d.DrivingLicenceNumber.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(d => d.SubmittedForReviewAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new AdminDriverListItemDto(
                d.DriverId, d.Name, d.PhoneNumber, d.City, d.ProfilePhotoUrl, d.Status, d.StatusReason,
                d.Status == DriverStatus.Pending && d.SubmittedForReviewAt != null,
                d.SubmittedForReviewAt, d.ApprovedAt,
                d.Cars.Count, d.Cars.Count(c => c.Status == CarStatus.Approved), d.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminDriverListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<AdminDriverDetailDto> GetDriverAsync(int driverId, CancellationToken cancellationToken = default)
    {
        var driver = await db.Drivers.AsNoTracking().Include(d => d.Documents).FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");
        var cars = await ListCarItemsAsync(db.Cars.AsNoTracking().Where(c => c.DriverId == driverId).OrderByDescending(c => c.CreatedAt), cancellationToken);
        var history = await HistoryAsync(e => e.EntityType == CarAuditEntity.Driver && e.EntityId == driverId, cancellationToken);
        return new AdminDriverDetailDto(DriverService.ToDto(driver), driver.EmailVerified ? driver.Email : null, driver.AddedByAdminId is not null, cars, history);
    }

    public async Task<AdminDriverDetailDto> CreateDriverAsync(int adminId, AdminCreateDriverRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneOtpService.NormalizeValidPhone(request.PhoneNumber);
        if (await db.Drivers.AnyAsync(d => d.PhoneNumber == phone, cancellationToken))
        {
            throw new ConflictException("A driver with this WhatsApp number already exists. Search for them instead.");
        }

        var now = DateTime.UtcNow;
        // Starts Pending. Because an admin added them, they can be approved straight away (see ApproveDriverAsync).
        var driver = new Driver
        {
            AddedByAdminId = adminId,
            Name = request.Name.Trim(),
            PhoneNumber = phone,
            City = string.IsNullOrWhiteSpace(request.City) ? null : CarRules.NormalizeCity(request.City),
            Status = DriverStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Drivers.Add(driver);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            throw new ConflictException("A driver with this WhatsApp number already exists. Search for them instead.");
        }

        CarAudit.Record(db, CarAuditEntity.Driver, driver.DriverId, "DriverCreated", "Driver added by admin", CarActor.Admin(adminId),
            newValue: $"{driver.Name} · {phone}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDriverAsync(driver.DriverId, cancellationToken);
    }

    public async Task<AdminDriverDetailDto> ApproveDriverAsync(int adminId, int driverId, string? note, CancellationToken cancellationToken = default)
    {
        var driver = await LoadDriverAsync(driverId, cancellationToken);
        var reinstating = driver.Status == DriverStatus.Suspended;
        if (driver.Status == DriverStatus.Approved)
        {
            throw new ConflictException("This driver is already approved.");
        }
        // A driver the admin added themselves needs no submitted profile — the admin vouches for them.
        var addedByAdmin = driver.AddedByAdminId is not null && driver.Status == DriverStatus.Pending;
        if (!addedByAdmin && (driver.Status == DriverStatus.Rejected || (driver.Status == DriverStatus.Pending && driver.SubmittedForReviewAt is null)))
        {
            throw new ConflictException("The driver hasn't sent their profile for review yet.");
        }
        if (!reinstating && !addedByAdmin)
        {
            var missing = CarMapping.MissingForReview(driver);
            if (missing.Count > 0)
            {
                throw new ValidationAppException(missing.Select(m => "Driver profile incomplete: " + m));
            }
        }

        var old = driver.Status;
        var now = DateTime.UtcNow;
        driver.Status = DriverStatus.Approved;
        driver.StatusReason = null;
        driver.ApprovedAt ??= now;
        driver.ReviewedByAdminId = adminId;
        driver.UpdatedAt = now;
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, reinstating ? "DriverReinstated" : "DriverApproved",
            reinstating ? "Driver reinstated" : "Driver approved", CarActor.Admin(adminId), old.ToString(), nameof(DriverStatus.Approved), Clean(note));
        await db.SaveChangesAsync(cancellationToken);
        return await GetDriverAsync(driverId, cancellationToken);
    }

    public async Task<AdminDriverDetailDto> RejectDriverAsync(int adminId, int driverId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = RequireReason(reason);
        var driver = await LoadDriverAsync(driverId, cancellationToken);
        if (driver.Status != DriverStatus.Pending)
        {
            throw new ConflictException(driver.Status == DriverStatus.Approved
                ? "This driver is already approved — suspend them instead."
                : $"This driver is {driver.Status.ToString().ToLowerInvariant()}.");
        }

        driver.Status = DriverStatus.Rejected;
        driver.StatusReason = why;
        // They fix what's wrong and send it again.
        driver.SubmittedForReviewAt = null;
        driver.ReviewedByAdminId = adminId;
        driver.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverRejected", "Driver rejected", CarActor.Admin(adminId),
            nameof(DriverStatus.Pending), nameof(DriverStatus.Rejected), why);
        await db.SaveChangesAsync(cancellationToken);
        return await GetDriverAsync(driverId, cancellationToken);
    }

    public async Task<AdminDriverDetailDto> SuspendDriverAsync(int adminId, int driverId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = RequireReason(reason);
        var driver = await LoadDriverAsync(driverId, cancellationToken);
        if (driver.Status == DriverStatus.Suspended)
        {
            throw new ConflictException("This driver is already suspended.");
        }

        var old = driver.Status;
        driver.Status = DriverStatus.Suspended;
        driver.StatusReason = why;
        driver.ReviewedByAdminId = adminId;
        driver.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverSuspended", "Driver suspended — cars hidden from search",
            CarActor.Admin(adminId), old.ToString(), nameof(DriverStatus.Suspended), why);
        await db.SaveChangesAsync(cancellationToken);
        return await GetDriverAsync(driverId, cancellationToken);
    }

    // ---------------- Cars ----------------

    public async Task<PagedResult<AdminCarListItemDto>> ListCarsAsync(AdminCarFilter filter, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        var query = db.Cars.AsNoTracking();
        query = filter switch
        {
            AdminCarFilter.Review => query.Where(c => c.Status == CarStatus.Pending && c.SubmittedForReviewAt != null),
            AdminCarFilter.Draft => query.Where(c => c.Status == CarStatus.Pending && c.SubmittedForReviewAt == null),
            AdminCarFilter.Approved => query.Where(c => c.Status == CarStatus.Approved),
            AdminCarFilter.Rejected => query.Where(c => c.Status == CarStatus.Rejected),
            AdminCarFilter.Suspended => query.Where(c => c.Status == CarStatus.Suspended),
            AdminCarFilter.Inactive => query.Where(c => c.Status == CarStatus.Inactive),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var plate = CarRules.NormalizeRegistration(term);
            query = query.Where(c => c.ModelName.Contains(term) || c.Brand.Contains(term) || c.Driver.Name.Contains(term)
                || (plate != "" && c.RegistrationNumber.Contains(plate)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ListCarItemsAsync(query.OrderByDescending(c => c.SubmittedForReviewAt ?? c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize),
            cancellationToken);
        return new PagedResult<AdminCarListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<AdminCarDetailDto> GetCarAsync(int carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.AsNoTracking()
            .Include(c => c.Driver)
            .Include(c => c.Photos)
            .Include(c => c.Pricings).ThenInclude(p => p.Tiers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CarId == carId, cancellationToken)
            ?? throw new NotFoundException("Car not found.");

        var documents = await db.DriverDocuments.AsNoTracking()
            .Where(d => d.CarId == carId || (d.DriverId == car.DriverId && d.CarId == null))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
        var upcoming = await db.CarBookings.Where(b => b.CarId == carId).Upcoming(DateTime.UtcNow).CountAsync(cancellationToken);
        var history = await HistoryAsync(e => (e.EntityType == CarAuditEntity.Car || e.EntityType == CarAuditEntity.Pricing) && e.EntityId == carId, cancellationToken);

        return new AdminCarDetailDto(
            CarMapping.ToDriverCarDto(car),
            new AdminDriverSummaryDto(car.Driver.DriverId, car.Driver.Name, car.Driver.PhoneNumber, car.Driver.ProfilePhotoUrl, car.Driver.Status),
            documents.Select(CarMapping.ToDto).ToList(),
            car.Pricings.OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.CarPricingId).Select(CarMapping.ToDto).ToList(),
            NotListedReasons(car),
            upcoming,
            history);
    }

    public async Task<AdminCarDetailDto> ApproveCarAsync(int adminId, int carId, string? note, CancellationToken cancellationToken = default)
    {
        var car = await LoadCarAsync(carId, cancellationToken);
        if (car.Status == CarStatus.Approved)
        {
            throw new ConflictException("This car is already approved.");
        }
        if (car.Status is CarStatus.Pending or CarStatus.Rejected && car.SubmittedForReviewAt is null)
        {
            throw new ConflictException("The driver hasn't sent this car for review yet.");
        }
        var missing = CarMapping.MissingForReview(car);
        if (missing.Count > 0)
        {
            throw new ValidationAppException(missing.Select(m => "Car incomplete: " + m));
        }

        var old = car.Status;
        var now = DateTime.UtcNow;
        car.Status = CarStatus.Approved;
        car.StatusReason = null;
        car.ApprovedAt ??= now;
        car.ReviewedByAdminId = adminId;
        car.UpdatedAt = now;
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarApproved", "Car approved", CarActor.Admin(adminId), old.ToString(), nameof(CarStatus.Approved), Clean(note));
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(carId, cancellationToken);
    }

    public async Task<AdminCarDetailDto> RejectCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = RequireReason(reason);
        var car = await LoadCarAsync(carId, cancellationToken);
        if (car.Status != CarStatus.Pending)
        {
            throw new ConflictException(car.Status == CarStatus.Approved
                ? "This car is already approved — suspend or deactivate it instead."
                : $"This car is {car.Status.ToString().ToLowerInvariant()}.");
        }

        car.Status = CarStatus.Rejected;
        car.StatusReason = why;
        car.SubmittedForReviewAt = null;
        car.ReviewedByAdminId = adminId;
        car.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarRejected", "Car rejected", CarActor.Admin(adminId), nameof(CarStatus.Pending), nameof(CarStatus.Rejected), why);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(carId, cancellationToken);
    }

    public Task<AdminCarDetailDto> SuspendCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default) =>
        TakeOffSiteAsync(adminId, carId, reason, CarStatus.Suspended, "CarSuspended", "Car suspended", cancellationToken);

    public Task<AdminCarDetailDto> DeactivateCarAsync(int adminId, int carId, string? reason, CancellationToken cancellationToken = default) =>
        TakeOffSiteAsync(adminId, carId, reason, CarStatus.Inactive, "CarDeactivated", "Car deactivated by admin", cancellationToken);

    private async Task<AdminCarDetailDto> TakeOffSiteAsync(int adminId, int carId, string? reason, CarStatus target, string action, string title, CancellationToken cancellationToken)
    {
        var why = RequireReason(reason);
        var car = await LoadCarAsync(carId, cancellationToken);
        if (car.Status == target)
        {
            throw new ConflictException($"This car is already {target.ToString().ToLowerInvariant()}.");
        }

        var old = car.Status;
        car.Status = target;
        car.StatusReason = why;
        car.SubmittedForReviewAt = null;
        car.ReviewedByAdminId = adminId;
        car.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Car, carId, action, title, CarActor.Admin(adminId), old.ToString(), target.ToString(), why);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(carId, cancellationToken);
    }

    // ---------------- Pricing ----------------

    public async Task<IReadOnlyList<AdminPendingPricingDto>> ListPendingPricingAsync(CancellationToken cancellationToken = default)
    {
        var carIds = await db.CarPricings.Where(p => p.Status == CarPricingStatus.Pending).Select(p => p.CarId).Distinct().ToListAsync(cancellationToken);
        var cars = await db.Cars.AsNoTracking()
            .Include(c => c.Driver)
            .Include(c => c.Pricings.Where(p => p.Status == CarPricingStatus.Pending || p.Status == CarPricingStatus.Approved)).ThenInclude(p => p.Tiers)
            .AsSplitQuery()
            .Where(c => carIds.Contains(c.CarId))
            .ToListAsync(cancellationToken);

        return cars
            .Select(c =>
            {
                var proposed = c.Pricings.Where(p => p.Status == CarPricingStatus.Pending).MaxBy(p => p.SubmittedAt)!;
                var current = c.Pricings.FirstOrDefault(p => p.CarPricingId == c.ActivePricingId);
                return new AdminPendingPricingDto(c.CarId, CarRules.DisplayName(c.Brand, c.ModelName), CarRules.Category(c.SeatCapacity),
                    c.RegistrationNumber, c.Status, c.DriverId, c.Driver.Name, CarMapping.ToDto(proposed), current is null ? null : CarMapping.ToDto(current));
            })
            .OrderBy(p => p.Proposed.SubmittedAt)
            .ToList();
    }

    public async Task<AdminCarDetailDto> ApprovePricingAsync(int adminId, int pricingId, CancellationToken cancellationToken = default)
    {
        var carId = await db.CarPricings.Where(p => p.CarPricingId == pricingId).Select(p => (int?)p.CarId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Pricing not found.");
        var car = await LoadCarAsync(carId, cancellationToken);
        var pricing = car.Pricings.Single(p => p.CarPricingId == pricingId);
        if (pricing.Status != CarPricingStatus.Pending)
        {
            throw new ConflictException($"This pricing is already {pricing.Status.ToString().ToLowerInvariant()}.");
        }

        Activate(car, pricing, adminId);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(carId, cancellationToken);
    }

    public async Task<AdminCarDetailDto> RejectPricingAsync(int adminId, int pricingId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = RequireReason(reason);
        var pricing = await db.CarPricings.Include(p => p.Tiers).FirstOrDefaultAsync(p => p.CarPricingId == pricingId, cancellationToken)
            ?? throw new NotFoundException("Pricing not found.");
        if (pricing.Status != CarPricingStatus.Pending)
        {
            throw new ConflictException($"This pricing is already {pricing.Status.ToString().ToLowerInvariant()}.");
        }

        pricing.Status = CarPricingStatus.Rejected;
        pricing.ReviewedAt = DateTime.UtcNow;
        pricing.ReviewedByAdminId = adminId;
        pricing.ReviewNote = why;
        CarAudit.Record(db, CarAuditEntity.Pricing, pricing.CarId, "PricingRejected", "Pricing rejected", CarActor.Admin(adminId),
            newValue: CarPricingProposals.Summarize(pricing), note: why);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(pricing.CarId, cancellationToken);
    }

    public async Task<AdminCarDetailDto> SetPricingAsync(int adminId, int carId, SubmitPricingRequest request, CancellationToken cancellationToken = default)
    {
        var car = await LoadCarAsync(carId, cancellationToken);
        var pricing = CarPricingProposals.Propose(db, car, request, CarActor.Admin(adminId));
        Activate(car, pricing, adminId);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCarAsync(carId, cancellationToken);
    }

    /// <summary>Makes <paramref name="pricing"/> the car's active pricing; the previous one is superseded (kept, not deleted).</summary>
    private void Activate(Car car, CarPricing pricing, int adminId)
    {
        var now = DateTime.UtcNow;
        var previous = car.ActivePricingId is null ? null : car.Pricings.FirstOrDefault(p => p.CarPricingId == car.ActivePricingId);
        if (previous is not null && previous != pricing)
        {
            previous.Status = CarPricingStatus.Superseded;
        }

        pricing.Status = CarPricingStatus.Approved;
        pricing.ReviewedAt = now;
        pricing.ReviewedByAdminId = adminId;
        pricing.ReviewNote = null;
        car.ActivePricing = pricing;
        car.UpdatedAt = now;

        CarAudit.Record(db, CarAuditEntity.Pricing, car.CarId, "PricingApproved", "Pricing approved and now active", CarActor.Admin(adminId),
            previous is null ? null : CarPricingProposals.Summarize(previous), CarPricingProposals.Summarize(pricing));
    }

    // ---------------- Dashboard ----------------

    public async Task<CarAdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var drivers = await db.Drivers.GroupBy(d => new { d.Status, Submitted = d.SubmittedForReviewAt != null })
            .Select(g => new { g.Key.Status, g.Key.Submitted, Count = g.Count() }).ToListAsync(cancellationToken);
        var cars = await db.Cars.GroupBy(c => new { c.Status, Submitted = c.SubmittedForReviewAt != null })
            .Select(g => new { g.Key.Status, g.Key.Submitted, Count = g.Count() }).ToListAsync(cancellationToken);

        var activeCars = await db.Cars.CountAsync(c => c.Status == CarStatus.Approved && c.Driver.Status == DriverStatus.Approved && c.ActivePricingId != null, cancellationToken);
        var pendingPricing = await db.CarPricings.CountAsync(p => p.Status == CarPricingStatus.Pending, cancellationToken);
        var upcoming = await db.CarBookings.CountAsync(b => b.Status == CarBookingStatus.Confirmed && b.EndsAt > now, cancellationToken);
        var inProgress = await db.CarBookings.CountAsync(b => b.Status == CarBookingStatus.InProgress, cancellationToken);
        var completed = await db.CarBookings.CountAsync(b => b.Status == CarBookingStatus.Completed, cancellationToken);
        var collected = await db.CarBookings.Where(b => b.PaidAt != null && b.PaymentStatus != CarPaymentStatus.Refunded)
            .SumAsync(b => (decimal?)b.BookingAmount, cancellationToken) ?? 0m;
        var fareTotal = await db.CarBookings.Where(b => b.Status == CarBookingStatus.Completed).SumAsync(b => b.FinalTotal, cancellationToken) ?? 0m;
        var refundsPending = await db.CarBookings.CountAsync(b => b.PaymentStatus == CarPaymentStatus.RefundPending, cancellationToken);

        var recent = await db.CarBookings.AsNoTracking()
            .Where(b => b.Status != CarBookingStatus.PendingPayment && b.Status != CarBookingStatus.Expired)
            .OrderByDescending(b => b.CreatedAt).Take(8)
            .Select(b => new
            {
                b.CarBookingId, b.BookingNumber, CustomerName = b.Customer.Name, b.Car.Brand, b.Car.ModelName, DriverName = b.Driver.Name,
                b.PickupAt, b.Status, b.PaymentStatus, b.EstimatedTotal, b.FinalTotal, b.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var driversAwaiting = drivers.Where(d => d.Status == DriverStatus.Pending && d.Submitted).Sum(d => d.Count);
        var carsAwaiting = cars.Where(c => c.Status == CarStatus.Pending && c.Submitted).Sum(c => c.Count);

        return new CarAdminDashboardDto(
            drivers.Sum(d => d.Count),
            driversAwaiting,
            drivers.Where(d => d.Status == DriverStatus.Approved).Sum(d => d.Count),
            cars.Sum(c => c.Count),
            carsAwaiting,
            cars.Where(c => c.Status == CarStatus.Approved).Sum(c => c.Count),
            activeCars,
            pendingPricing,
            driversAwaiting + carsAwaiting + pendingPricing,
            upcoming,
            inProgress,
            completed,
            collected,
            fareTotal,
            refundsPending,
            recent.Select(b => new CarBookingSummaryDto(b.CarBookingId, $"GC-{b.BookingNumber}", b.CustomerName,
                CarRules.DisplayName(b.Brand, b.ModelName), b.DriverName, b.PickupAt, b.Status, b.PaymentStatus, b.EstimatedTotal,
                b.FinalTotal, b.CreatedAt)).ToList());
    }

    // ---------------- helpers ----------------

    /// <summary>Runs the (already filtered, ordered, paged) car query as list items; the category label is added in memory.</summary>
    private static async Task<List<AdminCarListItemDto>> ListCarItemsAsync(IQueryable<Car> cars, CancellationToken cancellationToken)
    {
        var items = await ProjectCars(cars).ToListAsync(cancellationToken);
        return items.Select(c => c with { Category = CarRules.Category(c.SeatCapacity) }).ToList();
    }

    private static IQueryable<AdminCarListItemDto> ProjectCars(IQueryable<Car> cars) =>
        cars.Select(c => new AdminCarListItemDto(
            c.CarId,
            c.ModelName.StartsWith(c.Brand) ? c.ModelName : c.Brand + " " + c.ModelName,
            "",
            c.RegistrationNumber,
            c.SeatCapacity,
            c.FuelType,
            c.BaseCity,
            c.Photos.Where(p => p.Kind == CarPhotoKind.Exterior).OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).FirstOrDefault(),
            c.Status,
            c.StatusReason,
            c.Status == CarStatus.Pending && c.SubmittedForReviewAt != null,
            c.SubmittedForReviewAt,
            c.DriverId,
            c.Driver.Name,
            c.Driver.Status,
            c.Pricings.Any(p => p.Status == CarPricingStatus.Pending),
            c.Status == CarStatus.Approved && c.Driver.Status == DriverStatus.Approved && c.ActivePricingId != null,
            c.CreatedAt));

    private static List<string> NotListedReasons(Car car)
    {
        var reasons = new List<string>();
        if (car.Status != CarStatus.Approved)
        {
            reasons.Add($"Car is {car.Status.ToString().ToLowerInvariant()}.");
        }
        if (car.Driver.Status != DriverStatus.Approved)
        {
            reasons.Add($"Driver is {car.Driver.Status.ToString().ToLowerInvariant()}.");
        }
        if (car.ActivePricingId is null)
        {
            reasons.Add("No approved pricing yet.");
        }
        return reasons;
    }

    private async Task<List<CarAuditEventDto>> HistoryAsync(System.Linq.Expressions.Expression<Func<CarAuditEvent, bool>> filter, CancellationToken cancellationToken) =>
        await db.CarAuditEvents.AsNoTracking().Where(filter)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.CarAuditEventId).Take(100)
            .Select(e => new CarAuditEventDto(e.CarAuditEventId, e.EntityType, e.Action, e.Title, e.OldValue, e.NewValue, e.Note, e.ActorRole, e.ActorId, e.CreatedAt))
            .ToListAsync(cancellationToken);

    private async Task<Driver> LoadDriverAsync(int driverId, CancellationToken cancellationToken) =>
        await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");

    private async Task<Car> LoadCarAsync(int carId, CancellationToken cancellationToken) =>
        await db.Cars
            .Include(c => c.Driver)
            .Include(c => c.Photos)
            .Include(c => c.Pricings).ThenInclude(p => p.Tiers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CarId == carId, cancellationToken)
            ?? throw new NotFoundException("Car not found.");

    private static string RequireReason(string? reason) =>
        Clean(reason) ?? throw new ValidationAppException(["Please give a reason — the driver will see it."]);

    private static string? Clean(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static (int Page, int PageSize) Clamp(int page, int pageSize) => (Math.Max(page, 1), Math.Clamp(pageSize, 1, 100));
}
