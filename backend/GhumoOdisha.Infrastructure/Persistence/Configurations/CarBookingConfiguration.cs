using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarBookingConfiguration : IEntityTypeConfiguration<CarBooking>
{
    public void Configure(EntityTypeBuilder<CarBooking> builder)
    {
        builder.ToTable("CarBookings", t =>
        {
            t.HasCheckConstraint("CK_CarBooking_Duration", "DurationHours > 0");
            t.HasCheckConstraint("CK_CarBooking_Window", "EndsAt > PickupAt");
            t.HasCheckConstraint("CK_CarBooking_EstimatedKm", "EstimatedKm >= 0");
            t.HasCheckConstraint("CK_CarBooking_EstimatedNights", "EstimatedNights >= 0");
            t.HasCheckConstraint("CK_CarBooking_BookingAmount", "BookingAmount >= 0");
            t.HasCheckConstraint("CK_CarBooking_FinalKm", "FinalKm IS NULL OR FinalKm >= 0");
            t.HasCheckConstraint("CK_CarBooking_AdditionalCharges", "AdditionalCharges IS NULL OR AdditionalCharges >= 0");
        });

        builder.HasKey(b => b.CarBookingId);

        foreach (var money in new[]
        {
            nameof(CarBooking.EstimatedBaseFare), nameof(CarBooking.EstimatedKmCharge), nameof(CarBooking.EstimatedNightHaltCharge),
            nameof(CarBooking.EstimatedTotal), nameof(CarBooking.BookingAmount), nameof(CarBooking.FinalBaseFare),
            nameof(CarBooking.FinalKmCharge), nameof(CarBooking.FinalNightHaltCharge), nameof(CarBooking.AdditionalCharges),
            nameof(CarBooking.FinalTotal), nameof(CarBooking.BalanceDue), nameof(CarBooking.RefundAmount)
        })
        {
            builder.Property(money).HasColumnType("decimal(10,2)");
        }

        foreach (var timestamp in new[]
        {
            nameof(CarBooking.PickupAt), nameof(CarBooking.EndsAt), nameof(CarBooking.BalanceCollectedAt),
            nameof(CarBooking.HoldExpiresAt), nameof(CarBooking.PaidAt), nameof(CarBooking.RefundIssuedAt),
            nameof(CarBooking.RefundSettledAt), nameof(CarBooking.ConfirmedAt), nameof(CarBooking.CancelledAt),
            nameof(CarBooking.CompletedAt), nameof(CarBooking.CreatedAt), nameof(CarBooking.UpdatedAt)
        })
        {
            builder.Property(timestamp).HasColumnType("datetime(6)");
        }

        builder.Property(b => b.Status).HasConversion<int>();
        builder.Property(b => b.PaymentStatus).HasConversion<int>();
        builder.Property(b => b.RefundMethod).HasConversion<int?>();

        builder.Property(b => b.PickupCity).IsRequired().HasMaxLength(100);
        builder.Property(b => b.PickupAddress).HasMaxLength(500);
        builder.Property(b => b.AdditionalChargesNote).HasMaxLength(300);
        builder.Property(b => b.RazorpayOrderId).HasMaxLength(64);
        builder.Property(b => b.RazorpayPaymentId).HasMaxLength(64);
        builder.Property(b => b.RazorpayRefundId).HasMaxLength(64);
        builder.Property(b => b.RefundReference).HasMaxLength(120);
        builder.Property(b => b.CustomerNotes).HasMaxLength(1000);
        builder.Property(b => b.AdminNotes).HasColumnType("text");
        builder.Property(b => b.CancelledBy).HasMaxLength(20);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        builder.Ignore(b => b.Reference);
        builder.HasIndex(b => b.BookingNumber).IsUnique();
        builder.HasIndex(b => new { b.CustomerId, b.ClientRequestId }).IsUnique();
        // Availability check: active bookings of one car overlapping a window.
        builder.HasIndex(b => new { b.CarId, b.Status, b.PickupAt, b.EndsAt });
        builder.HasIndex(b => new { b.DriverId, b.Status });
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.PaymentStatus);
        builder.HasIndex(b => b.CreatedAt);

        builder.HasOne(b => b.Customer).WithMany().HasForeignKey(b => b.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Car).WithMany(c => c.Bookings).HasForeignKey(b => b.CarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Driver).WithMany().HasForeignKey(b => b.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.CarPricing).WithMany().HasForeignKey(b => b.CarPricingId).OnDelete(DeleteBehavior.Restrict);
    }
}
