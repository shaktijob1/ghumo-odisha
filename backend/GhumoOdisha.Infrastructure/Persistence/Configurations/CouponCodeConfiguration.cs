using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CouponCodeConfiguration : IEntityTypeConfiguration<CouponCode>
{
    public void Configure(EntityTypeBuilder<CouponCode> builder)
    {
        builder.ToTable("CouponCodes", t =>
        {
            t.HasCheckConstraint("CK_CouponCode_DiscountAmount_Positive", "DiscountAmount > 0");
        });

        builder.HasKey(c => c.CouponCodeId);

        builder.Property(c => c.Code).HasMaxLength(32).IsRequired();
        builder.Property(c => c.DiscountAmount).HasColumnType("decimal(10,2)");
        builder.Property(c => c.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(c => c.Code).IsUnique();
    }
}
