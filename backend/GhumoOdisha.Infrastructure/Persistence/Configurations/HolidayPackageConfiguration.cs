using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class HolidayPackageConfiguration : IEntityTypeConfiguration<HolidayPackage>
{
    public void Configure(EntityTypeBuilder<HolidayPackage> builder)
    {
        builder.ToTable("HolidayPackages", t =>
        {
            t.HasCheckConstraint("CK_HolidayPackage_PriceFrom", "PriceFrom IS NULL OR PriceFrom >= 0");
            t.HasCheckConstraint("CK_HolidayPackage_Duration", "Days >= 1 AND Nights >= 0");
        });
        builder.HasKey(p => p.HolidayPackageId);

        builder.Property(p => p.Title).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Summary).HasMaxLength(1000);
        builder.Property(p => p.Highlights).HasMaxLength(2000);
        builder.Property(p => p.PriceFrom).HasColumnType("decimal(10,2)");
        builder.Property(p => p.CoverImageUrl).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.IsActive, p.DisplayOrder });
    }
}
