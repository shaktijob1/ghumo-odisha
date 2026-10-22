using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CustomerTripInterestConfiguration : IEntityTypeConfiguration<CustomerTripInterest>
{
    public void Configure(EntityTypeBuilder<CustomerTripInterest> builder)
    {
        builder.ToTable("CustomerTripInterests", t =>
        {
            t.HasCheckConstraint("CK_CustomerTripInterest_Travellers", "Travellers > 0");
            t.HasCheckConstraint("CK_CustomerTripInterest_ExpectedAmount", "ExpectedAmount IS NULL OR ExpectedAmount >= 0");
        });

        builder.HasKey(i => i.CustomerTripInterestId);

        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.LostReason).HasMaxLength(500);
        builder.Property(i => i.ExpectedAmount).HasColumnType("decimal(10,2)");
        builder.Property(i => i.InterestedAt).HasColumnType("datetime(6)");
        builder.Property(i => i.LastContactAt).HasColumnType("datetime(6)");
        builder.Property(i => i.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(i => i.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(i => i.CustomerId);
        builder.HasIndex(i => new { i.TripId, i.TripDateSlotId });
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InterestedAt);
        // One interest per booking: also what keeps the website-booking sync from adding duplicates.
        // MySQL unique indexes allow many NULLs, so interests without a booking coexist.
        builder.HasIndex(i => i.BookingId).IsUnique();

        builder.HasOne(i => i.Customer).WithMany(c => c.TripInterests).HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Trip).WithMany().HasForeignKey(i => i.TripId).OnDelete(DeleteBehavior.Restrict);
        // A deleted departure leaves the interest on the trip, without a date.
        builder.HasOne(i => i.TripDateSlot).WithMany().HasForeignKey(i => i.TripDateSlotId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.Booking).WithMany().HasForeignKey(i => i.BookingId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.CreatedByAdminUser).WithMany().HasForeignKey(i => i.CreatedByAdminUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
