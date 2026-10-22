using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingPaymentOrderConfiguration : IEntityTypeConfiguration<BookingPaymentOrder>
{
    public void Configure(EntityTypeBuilder<BookingPaymentOrder> builder)
    {
        builder.ToTable("BookingPaymentOrders");
        builder.HasKey(o => o.BookingPaymentOrderId);

        builder.Property(o => o.RazorpayOrderId).IsRequired().HasMaxLength(100);
        builder.Property(o => o.AdvanceAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.DiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.CouponDiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.ConfirmedPaymentId).HasMaxLength(100);
        builder.Property(o => o.RefundedPaymentId).HasMaxLength(100);
        builder.Property(o => o.RazorpayRefundId).HasMaxLength(100);
        builder.Property(o => o.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(o => o.LastCheckedAt).HasColumnType("datetime(6)");

        builder.HasIndex(o => o.RazorpayOrderId).IsUnique();
        builder.HasIndex(o => new { o.BookingId, o.CreatedAt });

        builder.HasOne(o => o.Booking)
            .WithMany(b => b.PaymentOrders)
            .HasForeignKey(o => o.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
