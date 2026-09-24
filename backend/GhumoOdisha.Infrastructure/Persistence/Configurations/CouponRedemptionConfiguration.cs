using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("CouponRedemptions");

        builder.HasKey(r => r.CouponRedemptionId);

        builder.Property(r => r.DiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(r => r.CommissionAmount).HasColumnType("decimal(10,2)");
        builder.Property(r => r.RedeemedAt).HasColumnType("datetime(6)");

        // The actual enforcement of "one redemption per customer per coupon" — not application logic alone.
        builder.HasIndex(r => new { r.CouponCodeId, r.CustomerId }).IsUnique();

        builder.HasOne(r => r.CouponCode).WithMany(c => c.Redemptions).HasForeignKey(r => r.CouponCodeId);
        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId);
        builder.HasOne(r => r.Booking).WithMany().HasForeignKey(r => r.BookingId);
    }
}
