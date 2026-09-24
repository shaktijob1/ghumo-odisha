using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GhumoOdisha.Application.Common;

public interface IGhumoOdishaDbContext
{
    DatabaseFacade Database { get; }

    DbSet<Trip> Trips { get; }
    DbSet<TripPhoto> TripPhotos { get; }
    DbSet<TripHighlight> TripHighlights { get; }
    DbSet<RoomPhoto> RoomPhotos { get; }
    DbSet<VehiclePhoto> VehiclePhotos { get; }
    DbSet<PickupPoint> PickupPoints { get; }
    DbSet<OrganizerPhoto> OrganizerPhotos { get; }
    DbSet<SiteHeroPhoto> SiteHeroPhotos { get; }
    DbSet<TermsAcceptance> TermsAcceptances { get; }
    DbSet<ItineraryDay> ItineraryDays { get; }
    DbSet<ItineraryPoint> ItineraryPoints { get; }
    DbSet<TripDateSlot> TripDateSlots { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Customer> Customers { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<CustomerOtp> CustomerOtps { get; }
    DbSet<CustomerRefreshToken> CustomerRefreshTokens { get; }
    DbSet<CouponCode> CouponCodes { get; }
    DbSet<CouponRedemption> CouponRedemptions { get; }
    DbSet<Destination> Destinations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
