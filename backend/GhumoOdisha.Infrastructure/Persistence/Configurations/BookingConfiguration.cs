using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", t =>
        {
            t.HasCheckConstraint("CK_Booking_NumberOfSeats", "NumberOfSeats > 0");
            t.HasCheckConstraint("CK_Booking_AdvanceAmount_NonNegative", "AdvanceAmount >= 0");
            // An admin reducing seats after payment can leave AmountPaid above the new total — no
            // refund is given, the balance just floors at zero. So paid <= total is no longer an invariant.
            t.HasCheckConstraint("CK_Booking_RemainingAmount", "RemainingAmount = GREATEST(TotalAmount - AdvanceAmount, 0)");
            t.HasCheckConstraint("CK_Booking_GenderCounts", "COALESCE(MaleCount, 0) + COALESCE(FemaleCount, 0) <= NumberOfSeats");
        });

        builder.HasKey(b => b.BookingId);

        builder.Property(b => b.AmountPerPerson).HasColumnType("decimal(10,2)");
        builder.Property(b => b.TotalAmount).HasColumnType("decimal(10,2)");
        builder.Property(b => b.DiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(b => b.AdvanceAmount).HasColumnType("decimal(10,2)");
        builder.Property(b => b.RemainingAmount).HasColumnType("decimal(10,2)");

        builder.Property(b => b.BookingStatus).HasConversion<int>();
        builder.Property(b => b.PaymentStatus).HasConversion<int>();
        builder.Property(b => b.BookingSource).HasConversion<int>();

        builder.Property(b => b.CustomerNotes).HasColumnType("text");
        builder.Property(b => b.AdminNotes).HasColumnType("text");
        builder.Property(b => b.CancellationReason).HasMaxLength(500);
        builder.Property(b => b.RazorpayOrderId).HasMaxLength(64);
        builder.Property(b => b.RazorpayPaymentId).HasMaxLength(64);
        builder.Property(b => b.RazorpayRefundId).HasMaxLength(64);
        builder.Property(b => b.PendingAdvanceAmount).HasColumnType("decimal(10,2)");
        builder.Property(b => b.PendingDiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(b => b.PendingCouponDiscountAmount).HasColumnType("decimal(10,2)");

        builder.Property(b => b.RequestedAt).HasColumnType("datetime(6)");
        builder.Property(b => b.ConfirmedAt).HasColumnType("datetime(6)");
        builder.Property(b => b.CancelledAt).HasColumnType("datetime(6)");
        builder.Property(b => b.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(b => b.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(b => b.CustomerId);
        builder.HasIndex(b => b.TripId);
        builder.HasIndex(b => b.PickupPointId);
        builder.HasIndex(b => new { b.TripDateSlotId, b.BookingStatus });
        builder.HasIndex(b => b.BookingStatus);
        builder.HasIndex(b => b.PaymentStatus);
        builder.HasIndex(b => b.RequestedAt);
        builder.HasIndex(b => new { b.CustomerId, b.ClientRequestId }).IsUnique();
    }
}
