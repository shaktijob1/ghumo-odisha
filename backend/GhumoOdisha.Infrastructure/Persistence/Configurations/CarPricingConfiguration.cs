using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarPricingConfiguration : IEntityTypeConfiguration<CarPricing>
{
    public void Configure(EntityTypeBuilder<CarPricing> builder)
    {
        builder.ToTable("CarPricings", t =>
        {
            t.HasCheckConstraint("CK_CarPricing_PricePerKm", "PricePerKm >= 0");
            t.HasCheckConstraint("CK_CarPricing_NightHaltPrice", "NightHaltPrice >= 0");
        });

        builder.HasKey(p => p.CarPricingId);

        builder.Property(p => p.PricePerKm).HasColumnType("decimal(10,2)");
        builder.Property(p => p.NightHaltPrice).HasColumnType("decimal(10,2)");
        // Concurrency token: approving a proposal and the driver replacing it can't both win.
        builder.Property(p => p.Status).HasConversion<int>().IsConcurrencyToken();
        builder.Property(p => p.SubmittedByRole).IsRequired().HasMaxLength(20);
        builder.Property(p => p.ReviewNote).HasMaxLength(500);
        builder.Property(p => p.SubmittedAt).HasColumnType("datetime(6)");
        builder.Property(p => p.ReviewedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.CarId, p.Status });
        builder.HasIndex(p => p.Status);

        builder.HasMany(p => p.Tiers).WithOne(t => t.CarPricing).HasForeignKey(t => t.CarPricingId).OnDelete(DeleteBehavior.Cascade);
    }
}
