using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GhumoOdisha.Infrastructure.Persistence;

public class GhumoOdishaDbContext : DbContext, IGhumoOdishaDbContext
{
    public GhumoOdishaDbContext(DbContextOptions<GhumoOdishaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripPhoto> TripPhotos => Set<TripPhoto>();
    public DbSet<TripHighlight> TripHighlights => Set<TripHighlight>();
    public DbSet<RoomPhoto> RoomPhotos => Set<RoomPhoto>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<PickupPoint> PickupPoints => Set<PickupPoint>();
    public DbSet<OrganizerPhoto> OrganizerPhotos => Set<OrganizerPhoto>();
    public DbSet<SiteHeroPhoto> SiteHeroPhotos => Set<SiteHeroPhoto>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<ItineraryPoint> ItineraryPoints => Set<ItineraryPoint>();
    public DbSet<TripDateSlot> TripDateSlots => Set<TripDateSlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingPayment> BookingPayments => Set<BookingPayment>();
    public DbSet<BookingTraveller> BookingTravellers => Set<BookingTraveller>();
    public DbSet<BookingEvent> BookingEvents => Set<BookingEvent>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<CustomerOtp> CustomerOtps => Set<CustomerOtp>();
    public DbSet<BookingRefund> BookingRefunds => Set<BookingRefund>();
    public DbSet<CustomerRefreshToken> CustomerRefreshTokens => Set<CustomerRefreshToken>();
    public DbSet<CouponCode> CouponCodes => Set<CouponCode>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<CouponPayout> CouponPayouts => Set<CouponPayout>();
    public DbSet<AppLog> AppLogs => Set<AppLog>();
    public DbSet<AdminActivity> AdminActivities => Set<AdminActivity>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverRefreshToken> DriverRefreshTokens => Set<DriverRefreshToken>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();
    public DbSet<CarPricing> CarPricings => Set<CarPricing>();
    public DbSet<CarPricingTier> CarPricingTiers => Set<CarPricingTier>();
    public DbSet<CarBooking> CarBookings => Set<CarBooking>();
    public DbSet<CarTripExecution> CarTripExecutions => Set<CarTripExecution>();
    public DbSet<CarAuditEvent> CarAuditEvents => Set<CarAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GhumoOdishaDbContext).Assembly);

        // Every timestamp is stored in UTC, but MySQL's datetime has no zone, so values come back as
        // DateTimeKind.Unspecified and get serialized without a "Z" — browsers then read them as local
        // time (5½ h off in India). Mark them UTC on the way out so the API always sends "...Z".
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(utc);
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(utcNullable);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
