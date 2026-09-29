using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Auth;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Infrastructure.Storage;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Admin review of drivers, cars and pricing (Cars phase 3): only a full approval makes a car searchable.</summary>
[Collection("Cars")]
public class AdminCarApprovalTests : IDisposable
{
    private const int AdminId = 1;
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "go-admin-car-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _driverIds = [];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private LocalImageStorage Storage() => new(Options.Create(new ImageStorageOptions { BasePath = _storageRoot, PublicUrlPrefix = "/uploads" }));
    private static UploadedImage Jpeg() => new(new MemoryStream(JpegBytes), "p.jpg", "image/jpeg", JpegBytes.Length);

    private static SubmitPricingRequest Pricing(decimal perKm) =>
        new(perKm, 400m, [new CarPricingTierDto(100, 900m), new CarPricingTierDto(null, 600m)]);

    /// <summary>A driver with a complete, submitted profile and a submitted car with pending pricing.</summary>
    private async Task<(int DriverId, int CarId)> SubmittedDriverAndCarAsync(GhumoOdishaDbContext db)
    {
        var whatsApp = new FakeWhatsAppService();
        var auth = new DriverAuthService(db, TestServices.CreatePhoneOtpService(db, whatsApp),
            new JwtTokenService(Options.Create(TestServices.JwtSettings)), new FakeGoogleTokenValidator(),
            Options.Create(TestServices.JwtSettings), NullLogger<DriverAuthService>.Instance);
        var phone = TestDb.RandomPhoneNumber();
        await auth.RequestOtpAsync(new RequestOtpRequest("Approval Driver", phone));
        var driverId = (await auth.VerifyOtpAsync(new VerifyOtpRequest(phone, whatsApp.LastOtp!))).DriverId;
        _driverIds.Add(driverId);

        var profile = new DriverService(db, Storage());
        await profile.UpdateProfileAsync(driverId, new UpdateDriverProfileRequest("Approval Driver", null, "Lane 3", "Puri", "OD13 20150001234", null, 5));
        await profile.SetProfilePhotoAsync(driverId, Jpeg());
        await profile.UploadDocumentAsync(driverId, DriverDocumentType.DrivingLicence, null, Jpeg());
        await profile.SubmitForReviewAsync(driverId);

        var cars = new DriverCarService(db, Storage());
        var car = await cars.CreateAsync(driverId, new SaveCarRequest("Maruti", "Ertiga", $"OD13C{Random.Shared.Next(1000, 9999)}", FuelType.CNG, 7, true, null, "Puri"));
        await cars.AddPhotoAsync(driverId, car.CarId, CarPhotoKind.Exterior, Jpeg());
        await cars.AddPhotoAsync(driverId, car.CarId, CarPhotoKind.Interior, Jpeg());
        await cars.SubmitPricingAsync(driverId, car.CarId, Pricing(14m));
        await cars.SubmitForReviewAsync(driverId, car.CarId);
        return (driverId, car.CarId);
    }

    [Fact]
    public async Task Car_is_listed_only_after_driver_car_and_pricing_are_all_approved()
    {
        await using var db = TestDb.CreateContext();
        var (driverId, carId) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);

        var reviewQueue = await admin.ListCarsAsync(AdminCarFilter.Review, null, 1, 100);
        Assert.Contains(reviewQueue.Items, c => c.CarId == carId && c.AwaitingReview && c.HasPendingPricing && c.Category == "7 Seater Car");

        await admin.ApproveDriverAsync(AdminId, driverId, null);
        var afterCar = await admin.ApproveCarAsync(AdminId, carId, "Looks good");
        Assert.False(afterCar.Car.IsListed);
        Assert.Equal(["No approved pricing yet."], afterCar.NotListedReasons);

        var pending = (await admin.ListPendingPricingAsync()).Single(p => p.CarId == carId);
        Assert.Null(pending.Current);
        var afterPricing = await admin.ApprovePricingAsync(AdminId, pending.Proposed.CarPricingId);

        Assert.True(afterPricing.Car.IsListed);
        Assert.Empty(afterPricing.NotListedReasons);
        Assert.Equal(14m, afterPricing.Car.ActivePricing!.PricePerKm);
        Assert.Contains(afterPricing.History, h => h.Action == "CarApproved" && h.ActorRole == "Admin" && h.ActorId == AdminId && h.Note == "Looks good");
        Assert.Contains(afterPricing.History, h => h.Action == "PricingApproved");
    }

    [Fact]
    public async Task Rejected_pricing_can_be_resubmitted_and_approval_supersedes_the_old_active_version()
    {
        await using var db = TestDb.CreateContext();
        var (driverId, carId) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);
        var cars = new DriverCarService(db, Storage());
        var first = (await admin.ListPendingPricingAsync()).Single(p => p.CarId == carId).Proposed.CarPricingId;
        await admin.ApprovePricingAsync(AdminId, first);

        // Driver asks for more; admin says no, with a reason the driver sees.
        var raise = await cars.SubmitPricingAsync(driverId, carId, Pricing(20m));
        await Assert.ThrowsAsync<ValidationAppException>(() => admin.RejectPricingAsync(AdminId, raise.PendingPricing!.CarPricingId, "  "));
        await admin.RejectPricingAsync(AdminId, raise.PendingPricing!.CarPricingId, "Too high for a 7 seater.");
        var seen = await cars.GetAsync(driverId, carId);
        Assert.Equal(14m, seen.ActivePricing!.PricePerKm);
        Assert.Equal("Too high for a 7 seater.", seen.RejectedPricing!.ReviewNote);

        // Driver resubmits; approval makes it active and the old version is superseded, not lost.
        var retry = await cars.SubmitPricingAsync(driverId, carId, Pricing(16m));
        Assert.Null(retry.RejectedPricing);
        await admin.ApprovePricingAsync(AdminId, retry.PendingPricing!.CarPricingId);

        var history = await cars.GetPricingHistoryAsync(driverId, carId);
        Assert.Equal(3, history.Count);
        Assert.Equal(CarPricingStatus.Approved, history.Single(p => p.PricePerKm == 16m).Status);
        Assert.Equal(CarPricingStatus.Superseded, history.Single(p => p.PricePerKm == 14m).Status);
        Assert.Equal(CarPricingStatus.Rejected, history.Single(p => p.PricePerKm == 20m).Status);
        await Assert.ThrowsAsync<ConflictException>(() => admin.ApprovePricingAsync(AdminId, first));
    }

    [Fact]
    public async Task Admin_set_pricing_is_active_immediately_and_withdraws_the_drivers_pending_proposal()
    {
        await using var db = TestDb.CreateContext();
        var (_, carId) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);

        var result = await admin.SetPricingAsync(AdminId, carId, Pricing(12.5m));

        Assert.Equal(12.5m, result.Car.ActivePricing!.PricePerKm);
        Assert.Equal("Admin", result.Car.ActivePricing.SubmittedByRole);
        Assert.Null(result.Car.PendingPricing);
        Assert.Contains(result.PricingHistory, p => p.PricePerKm == 14m && p.Status == CarPricingStatus.Withdrawn);
    }

    [Fact]
    public async Task Suspending_the_driver_hides_their_approved_car_and_reinstating_brings_it_back()
    {
        await using var db = TestDb.CreateContext();
        var (driverId, carId) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);
        await admin.ApproveDriverAsync(AdminId, driverId, null);
        await admin.ApproveCarAsync(AdminId, carId, null);
        await admin.SetPricingAsync(AdminId, carId, Pricing(14m));
        Assert.True((await admin.GetCarAsync(carId)).Car.IsListed);

        await Assert.ThrowsAsync<ValidationAppException>(() => admin.SuspendDriverAsync(AdminId, driverId, null));
        await admin.SuspendDriverAsync(AdminId, driverId, "Customer complaint under review.");
        var hidden = await admin.GetCarAsync(carId);
        Assert.False(hidden.Car.IsListed);
        Assert.Contains("Driver is suspended.", hidden.NotListedReasons);

        // A suspended driver can't send anything for review.
        await Assert.ThrowsAsync<ConflictException>(() => new DriverService(db, Storage()).SubmitForReviewAsync(driverId));

        await admin.ApproveDriverAsync(AdminId, driverId, "Resolved.");
        Assert.True((await admin.GetCarAsync(carId)).Car.IsListed);
        var driverHistory = (await admin.GetDriverAsync(driverId)).History;
        Assert.Contains(driverHistory, h => h.Action == "DriverSuspended" && h.Note == "Customer complaint under review.");
        Assert.Contains(driverHistory, h => h.Action == "DriverReinstated");
    }

    [Fact]
    public async Task Rejected_car_must_be_resubmitted_before_it_can_be_approved()
    {
        await using var db = TestDb.CreateContext();
        var (driverId, carId) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);

        await admin.RejectCarAsync(AdminId, carId, "Interior photo is blurry.");
        await Assert.ThrowsAsync<ConflictException>(() => admin.ApproveCarAsync(AdminId, carId, null));

        var car = await new DriverCarService(db, Storage()).GetAsync(driverId, carId);
        Assert.Equal(CarStatus.Rejected, car.Status);
        Assert.Equal("Interior photo is blurry.", car.StatusReason);

        await new DriverCarService(db, Storage()).SubmitForReviewAsync(driverId, carId);
        Assert.Equal(CarStatus.Approved, (await admin.ApproveCarAsync(AdminId, carId, null)).Car.Status);
    }

    [Fact]
    public async Task A_stale_write_cannot_overwrite_an_admin_decision()
    {
        await using var driverSide = TestDb.CreateContext();
        var (_, carId) = await SubmittedDriverAndCarAsync(driverSide);
        var pricingId = await driverSide.CarPricings.Where(p => p.CarId == carId && p.Status == CarPricingStatus.Pending).Select(p => p.CarPricingId).SingleAsync();

        // The driver's request has the pending pricing loaded...
        var stale = await driverSide.CarPricings.SingleAsync(p => p.CarPricingId == pricingId);

        // ...an admin approves it in another request...
        await using (var adminSide = TestDb.CreateContext())
        {
            await new AdminCarService(adminSide).ApprovePricingAsync(AdminId, pricingId);
        }

        // ...so the driver's withdrawal of it must fail rather than silently undo the approval.
        stale.Status = CarPricingStatus.Withdrawn;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => driverSide.SaveChangesAsync());

        await using var check = TestDb.CreateContext();
        Assert.Equal(CarPricingStatus.Approved, (await check.CarPricings.SingleAsync(p => p.CarPricingId == pricingId)).Status);
    }

    [Fact]
    public async Task Dashboard_counts_the_approval_queues()
    {
        await using var db = TestDb.CreateContext();
        var admin = new AdminCarService(db);
        var before = await admin.GetDashboardAsync();

        await SubmittedDriverAndCarAsync(db);
        var after = await admin.GetDashboardAsync();

        Assert.Equal(before.DriversAwaitingReview + 1, after.DriversAwaitingReview);
        Assert.Equal(before.CarsAwaitingReview + 1, after.CarsAwaitingReview);
        Assert.Equal(before.PricingAwaitingReview + 1, after.PricingAwaitingReview);
        Assert.Equal(before.PendingApprovals + 3, after.PendingApprovals);
    }

    [Fact]
    public async Task Admin_adds_a_car_for_an_existing_driver_and_publishes_it_with_photos_and_pricing()
    {
        await using var db = TestDb.CreateContext();
        var (driverId, _) = await SubmittedDriverAndCarAsync(db);
        var admin = new AdminCarService(db);
        var cars = new DriverCarService(db, Storage());
        await admin.ApproveDriverAsync(AdminId, driverId, null);

        var plate = $"od 13 d {Random.Shared.Next(1000, 9999)}";
        var car = await cars.AdminCreateAsync(AdminId, driverId, new SaveCarRequest("Toyota", "Innova Crysta", plate, FuelType.Diesel, 7, true, null, "puri"));
        Assert.Equal(CarStatus.Pending, car.Status);
        Assert.NotNull(car.SubmittedForReviewAt);   // ready to publish, no driver submission needed
        Assert.Equal("Puri", car.BaseCity);

        // Same rules as the driver's own path: nothing is published until photos and pricing are in.
        await Assert.ThrowsAsync<ValidationAppException>(() => admin.ApproveCarAsync(AdminId, car.CarId, null));
        // …and the same number-plate uniqueness.
        await Assert.ThrowsAsync<ConflictException>(() =>
            cars.AdminCreateAsync(AdminId, driverId, new SaveCarRequest("Toyota", "Innova", plate.ToUpperInvariant(), FuelType.Diesel, 7, true, null, "Puri")));

        await cars.AdminAddPhotoAsync(AdminId, car.CarId, CarPhotoKind.Exterior, Jpeg());
        await cars.AdminAddPhotoAsync(AdminId, car.CarId, CarPhotoKind.Interior, Jpeg());
        await admin.SetPricingAsync(AdminId, car.CarId, Pricing(15m));
        var published = await admin.ApproveCarAsync(AdminId, car.CarId, null);

        Assert.True(published.Car.IsListed);
        Assert.Equal(driverId, published.Driver.DriverId);
        Assert.Contains(published.History, h => h.Action == "CarRegistered" && h.Title == "Car added by admin" && h.ActorRole == "Admin");
        Assert.Contains(published.History, h => h.Action == "CarPhotoAdded" && h.ActorRole == "Admin");
        // The driver sees it as their own car.
        Assert.Contains(await cars.ListAsync(driverId), c => c.CarId == car.CarId);
    }

    [Fact]
    public async Task Admin_added_driver_can_be_approved_directly_but_a_self_signup_cannot()
    {
        await using var db = TestDb.CreateContext();
        var admin = new AdminCarService(db);
        var phone = TestDb.RandomPhoneNumber();

        var added = await admin.CreateDriverAsync(AdminId, new AdminCreateDriverRequest("  Walk-in Owner ", "+91" + phone, "bhubaneswar"));
        _driverIds.Add(added.Profile.DriverId);
        Assert.Equal("Walk-in Owner", added.Profile.Name);
        Assert.Equal(phone, added.Profile.PhoneNumber);
        Assert.Equal("Bhubaneswar", added.Profile.City);
        Assert.Equal(DriverStatus.Pending, added.Profile.Status);
        Assert.Contains(added.History, h => h.Action == "DriverCreated" && h.ActorRole == "Admin");

        await Assert.ThrowsAsync<ConflictException>(() => admin.CreateDriverAsync(AdminId, new AdminCreateDriverRequest("Someone", phone, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() => admin.CreateDriverAsync(AdminId, new AdminCreateDriverRequest("Someone", "12345", null)));

        // A car added for them is listed only once the driver is approved…
        var cars = new DriverCarService(db, Storage());
        var car = await cars.AdminCreateAsync(AdminId, added.Profile.DriverId,
            new SaveCarRequest("Maruti", "Dzire", $"OD02E{Random.Shared.Next(1000, 9999)}", FuelType.Petrol, 5, true, null, "Bhubaneswar"));
        await cars.AdminAddPhotoAsync(AdminId, car.CarId, CarPhotoKind.Exterior, Jpeg());
        await cars.AdminAddPhotoAsync(AdminId, car.CarId, CarPhotoKind.Interior, Jpeg());
        await admin.SetPricingAsync(AdminId, car.CarId, Pricing(12m));
        Assert.False((await admin.ApproveCarAsync(AdminId, car.CarId, null)).Car.IsListed);

        // …and because the admin added them, that approval needs no submitted profile or licence.
        Assert.True(added.AddedByAdmin);
        var approved = await admin.ApproveDriverAsync(AdminId, added.Profile.DriverId, null);
        Assert.Equal(DriverStatus.Approved, approved.Profile.Status);
        Assert.True((await admin.GetCarAsync(car.CarId)).Car.IsListed);

        // A driver who signed up themselves still has to complete and submit their profile first.
        var whatsApp = new FakeWhatsAppService();
        var auth = new DriverAuthService(db, TestServices.CreatePhoneOtpService(db, whatsApp),
            new JwtTokenService(Options.Create(TestServices.JwtSettings)), new FakeGoogleTokenValidator(),
            Options.Create(TestServices.JwtSettings), NullLogger<DriverAuthService>.Instance);
        var selfPhone = TestDb.RandomPhoneNumber();
        await auth.RequestOtpAsync(new RequestOtpRequest("Self Signup", selfPhone));
        var selfId = (await auth.VerifyOtpAsync(new VerifyOtpRequest(selfPhone, whatsApp.LastOtp!))).DriverId;
        _driverIds.Add(selfId);
        await Assert.ThrowsAsync<ConflictException>(() => admin.ApproveDriverAsync(AdminId, selfId, null));
    }

    public void Dispose()
    {
        using (var db = TestDb.CreateContext())
        {
            var carIds = db.Cars.Where(c => _driverIds.Contains(c.DriverId)).Select(c => c.CarId).ToList();
            db.CarAuditEvents.Where(e => (e.EntityType == CarAuditEntity.Driver && _driverIds.Contains(e.EntityId))
                || (e.EntityType != CarAuditEntity.Driver && carIds.Contains(e.EntityId))).ExecuteDelete();
            db.Cars.Where(c => carIds.Contains(c.CarId)).ExecuteUpdate(s => s.SetProperty(c => c.ActivePricingId, (int?)null));
            db.DriverDocuments.Where(d => _driverIds.Contains(d.DriverId)).ExecuteDelete();
            db.Cars.Where(c => carIds.Contains(c.CarId)).ExecuteDelete();
            db.Drivers.Where(d => _driverIds.Contains(d.DriverId)).ExecuteDelete();
        }
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }
}
