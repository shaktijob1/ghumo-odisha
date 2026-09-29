using System.IdentityModel.Tokens.Jwt;
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

/// <summary>Driver sign-up, profile, car registration, photos, documents and pricing proposals (Cars phase 2).</summary>
[Collection("Cars")]
public class DriverCarTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "go-driver-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _driverIds = [];

    // Starts with the JPEG signature, which is all LocalImageStorage checks.
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private LocalImageStorage Storage() =>
        new(Options.Create(new ImageStorageOptions { BasePath = _storageRoot, PublicUrlPrefix = "/uploads" }));

    private static UploadedImage Jpeg() => new(new MemoryStream(JpegBytes), "photo.jpg", "image/jpeg", JpegBytes.Length);

    private static SaveCarRequest CarRequest(string? registration = null) => new(
        "Toyota", "Innova Crysta", registration ?? $"od 02 ab {Random.Shared.Next(1000, 9999)}",
        FuelType.Diesel, 7, true, "Clean, well kept.", "  bhubaneswar ");

    private static SubmitPricingRequest Pricing(decimal perKm = 15m) =>
        new(perKm, 400m, [new CarPricingTierDto(100, 900m), new CarPricingTierDto(200, 600m), new CarPricingTierDto(null, 600m)]);

    private async Task<DriverAuthResponse> SignUpDriverAsync(GhumoOdishaDbContext db)
    {
        var whatsApp = new FakeWhatsAppService();
        var auth = new DriverAuthService(
            db,
            TestServices.CreatePhoneOtpService(db, whatsApp),
            new JwtTokenService(Options.Create(TestServices.JwtSettings)),
            new FakeGoogleTokenValidator(),
            Options.Create(TestServices.JwtSettings),
            NullLogger<DriverAuthService>.Instance);

        var phone = TestDb.RandomPhoneNumber();
        await auth.RequestOtpAsync(new RequestOtpRequest("Ramesh Driver", phone));
        var result = await auth.VerifyOtpAsync(new VerifyOtpRequest(phone, whatsApp.LastOtp!));
        _driverIds.Add(result.DriverId);
        return result;
    }

    [Fact]
    public async Task Otp_sign_up_creates_a_pending_driver_with_a_driver_role_token()
    {
        await using var db = TestDb.CreateContext();

        var result = await SignUpDriverAsync(db);

        Assert.Equal(DriverStatus.Pending, result.Status);
        Assert.Equal("Ramesh Driver", result.Name);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Contains(token.Claims, c => c.Value == "Driver");
        Assert.DoesNotContain(token.Claims, c => c.Value is "Admin" or "Customer");

        var driver = await db.Drivers.AsNoTracking().SingleAsync(d => d.DriverId == result.DriverId);
        Assert.Null(driver.SubmittedForReviewAt);
    }

    [Fact]
    public async Task A_driver_cannot_see_or_change_another_drivers_car()
    {
        await using var db = TestDb.CreateContext();
        var owner = await SignUpDriverAsync(db);
        var other = await SignUpDriverAsync(db);
        var service = new DriverCarService(db, Storage());
        var car = await service.CreateAsync(owner.DriverId, CarRequest());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(other.DriverId, car.CarId));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(other.DriverId, car.CarId, CarRequest()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SubmitPricingAsync(other.DriverId, car.CarId, Pricing()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddPhotoAsync(other.DriverId, car.CarId, CarPhotoKind.Exterior, Jpeg()));
        Assert.Empty(await service.ListAsync(other.DriverId));
    }

    [Fact]
    public async Task Registration_is_normalised_and_unique_and_city_is_title_cased()
    {
        await using var db = TestDb.CreateContext();
        var driver = await SignUpDriverAsync(db);
        var service = new DriverCarService(db, Storage());
        var plate = $"od-02 ab {Random.Shared.Next(1000, 9999)}";

        var car = await service.CreateAsync(driver.DriverId, CarRequest(plate));

        Assert.Equal(CarRules.NormalizeRegistration(plate), car.RegistrationNumber);
        Assert.Matches("^OD02AB[0-9]{4}$", car.RegistrationNumber);
        Assert.Equal("Bhubaneswar", car.BaseCity);
        Assert.Equal("7 Seater Car", car.Category);
        Assert.Equal("Toyota Innova Crysta", car.DisplayName);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(driver.DriverId, CarRequest(car.RegistrationNumber.ToLowerInvariant())));
    }

    [Fact]
    public async Task Car_cannot_be_submitted_without_both_photo_kinds_and_pricing()
    {
        await using var db = TestDb.CreateContext();
        var driver = await SignUpDriverAsync(db);
        var service = new DriverCarService(db, Storage());
        var car = await service.CreateAsync(driver.DriverId, CarRequest());

        var error = await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitForReviewAsync(driver.DriverId, car.CarId));
        Assert.Equal(3, error.Errors.Count());

        await service.AddPhotoAsync(driver.DriverId, car.CarId, CarPhotoKind.Exterior, Jpeg());
        await service.AddPhotoAsync(driver.DriverId, car.CarId, CarPhotoKind.Interior, Jpeg());
        await service.SubmitPricingAsync(driver.DriverId, car.CarId, Pricing());
        var submitted = await service.SubmitForReviewAsync(driver.DriverId, car.CarId);

        Assert.Equal(CarStatus.Pending, submitted.Status);
        Assert.NotNull(submitted.SubmittedForReviewAt);
        Assert.False(submitted.IsListed);
        Assert.Empty(submitted.MissingForReview);

        // Once submitted, the last photo of a kind can't be removed.
        var lastInterior = submitted.Photos.Single(p => p.Kind == CarPhotoKind.Interior);
        await Assert.ThrowsAsync<ConflictException>(() => service.DeletePhotoAsync(driver.DriverId, car.CarId, lastInterior.CarPhotoId));
    }

    [Fact]
    public async Task New_pricing_is_pending_withdraws_the_previous_proposal_and_never_activates_itself()
    {
        await using var db = TestDb.CreateContext();
        var driver = await SignUpDriverAsync(db);
        var service = new DriverCarService(db, Storage());
        var car = await service.CreateAsync(driver.DriverId, CarRequest());

        await service.SubmitPricingAsync(driver.DriverId, car.CarId, Pricing(15m));
        var afterSecond = await service.SubmitPricingAsync(driver.DriverId, car.CarId, Pricing(14m));

        Assert.Null(afterSecond.ActivePricing);
        Assert.Equal(14m, afterSecond.PendingPricing!.PricePerKm);
        Assert.Equal(CarPricingStatus.Pending, afterSecond.PendingPricing.Status);

        var history = await service.GetPricingHistoryAsync(driver.DriverId, car.CarId);
        Assert.Equal(2, history.Count);
        Assert.Equal(CarPricingStatus.Withdrawn, history.Single(p => p.PricePerKm == 15m).Status);

        var stored = await db.Cars.AsNoTracking().SingleAsync(c => c.CarId == car.CarId);
        Assert.Null(stored.ActivePricingId);
        Assert.True(await db.CarAuditEvents.AnyAsync(e => e.EntityType == CarAuditEntity.Pricing && e.EntityId == car.CarId && e.Action == "PricingSubmitted"));
    }

    [Fact]
    public async Task Pricing_with_invalid_tiers_is_rejected()
    {
        await using var db = TestDb.CreateContext();
        var driver = await SignUpDriverAsync(db);
        var service = new DriverCarService(db, Storage());
        var car = await service.CreateAsync(driver.DriverId, CarRequest());

        var noOpenTier = new SubmitPricingRequest(15m, 400m, [new CarPricingTierDto(100, 900m)]);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitPricingAsync(driver.DriverId, car.CarId, noOpenTier));
    }

    [Fact]
    public async Task Driver_profile_needs_everything_before_review_and_documents_stay_private_to_their_owner()
    {
        await using var db = TestDb.CreateContext();
        var driver = await SignUpDriverAsync(db);
        var other = await SignUpDriverAsync(db);
        var service = new DriverService(db, Storage());

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitForReviewAsync(driver.DriverId));

        await service.UpdateProfileAsync(driver.DriverId,
            new UpdateDriverProfileRequest("Ramesh Driver", null, "Plot 12, Saheed Nagar", "bhubaneswar", "od02 20190012345", null, 8));
        await service.SetProfilePhotoAsync(driver.DriverId, Jpeg());
        var licence = await service.UploadDocumentAsync(driver.DriverId, DriverDocumentType.DrivingLicence, null, Jpeg());

        var submitted = await service.SubmitForReviewAsync(driver.DriverId);
        Assert.Equal(DriverStatus.Pending, submitted.Status);
        Assert.NotNull(submitted.SubmittedForReviewAt);
        Assert.Equal("OD02 20190012345", submitted.DrivingLicenceNumber);
        await Assert.ThrowsAsync<ConflictException>(() => service.SubmitForReviewAsync(driver.DriverId));

        var own = await service.OpenDocumentAsync(driver.DriverId, licence.DriverDocumentId);
        await using (own.Content)
        {
            Assert.Equal("image/jpeg", own.ContentType);
        }
        await Assert.ThrowsAsync<NotFoundException>(() => service.OpenDocumentAsync(other.DriverId, licence.DriverDocumentId));
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
