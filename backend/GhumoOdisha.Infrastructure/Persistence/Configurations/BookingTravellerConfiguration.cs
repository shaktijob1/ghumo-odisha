using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingTravellerConfiguration : IEntityTypeConfiguration<BookingTraveller>
{
    public void Configure(EntityTypeBuilder<BookingTraveller> builder)
    {
        builder.ToTable("BookingTravellers", t =>
        {
            t.HasCheckConstraint("CK_BookingTraveller_SeatNumber", "SeatNumber > 0");
            // Only ever the last four digits — a full Aadhaar number can't be stored by construction.
            t.HasCheckConstraint("CK_BookingTraveller_AadhaarLast4", "AadhaarLast4 IS NULL OR CHAR_LENGTH(AadhaarLast4) = 4");
        });

        builder.HasKey(t => t.BookingTravellerId);

        builder.Property(t => t.FullName).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Gender).HasConversion<int?>();
        builder.Property(t => t.AadhaarLast4).HasMaxLength(4);
        builder.Property(t => t.PhoneNumber).HasMaxLength(10);
        builder.Property(t => t.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(t => new { t.BookingId, t.SeatNumber }).IsUnique();
        builder.HasIndex(t => t.LinkedCustomerId);

        builder.HasOne(t => t.Booking).WithMany(b => b.Travellers).HasForeignKey(t => t.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.LinkedCustomer).WithMany().HasForeignKey(t => t.LinkedCustomerId).OnDelete(DeleteBehavior.SetNull);
    }
}
