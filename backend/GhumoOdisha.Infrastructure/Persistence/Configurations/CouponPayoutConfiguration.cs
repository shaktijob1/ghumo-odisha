using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CouponPayoutConfiguration : IEntityTypeConfiguration<CouponPayout>
{
    public void Configure(EntityTypeBuilder<CouponPayout> builder)
    {
        builder.ToTable("CouponPayouts", t =>
        {
            t.HasCheckConstraint("CK_CouponPayout_Amount_Positive", "Amount > 0");
        });

        builder.HasKey(p => p.CouponPayoutId);

        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        builder.Property(p => p.Method).HasConversion<int>();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.Property(p => p.PaidAt).HasColumnType("datetime(6)");
        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.CouponCodeId, p.PaidAt });

        // Restrict: payout history must survive — a coupon with payouts is deactivated, never deleted.
        builder.HasOne(p => p.CouponCode).WithMany(c => c.Payouts).HasForeignKey(p => p.CouponCodeId).OnDelete(DeleteBehavior.Restrict);
    }
}
