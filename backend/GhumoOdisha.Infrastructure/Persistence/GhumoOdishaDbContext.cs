using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<CustomerRefreshToken> CustomerRefreshTokens => Set<CustomerRefreshToken>();
    public DbSet<CouponCode> CouponCodes => Set<CouponCode>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<CouponPayout> CouponPayouts => Set<CouponPayout>();
    public DbSet<AppLog> AppLogs => Set<AppLog>();
    public DbSet<AdminActivity> AdminActivities => Set<AdminActivity>();
    public DbSet<Destination> Destinations => Set<Destination>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GhumoOdishaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
