using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarPricingTierConfiguration : IEntityTypeConfiguration<CarPricingTier>
{
    public void Configure(EntityTypeBuilder<CarPricingTier> builder)
    {
        builder.ToTable("CarPricingTiers", t =>
        {
            t.HasCheckConstraint("CK_CarPricingTier_UpToKm", "UpToKm IS NULL OR UpToKm > 0");
            t.HasCheckConstraint("CK_CarPricingTier_BaseFare", "BaseFare >= 0");
        });

        builder.HasKey(t => t.CarPricingTierId);

        builder.Property(t => t.BaseFare).HasColumnType("decimal(10,2)");

        builder.HasIndex(t => new { t.CarPricingId, t.UpToKm }).IsUnique();
    }
}
