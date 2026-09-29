using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.ToTable("Cars", t =>
        {
            t.HasCheckConstraint("CK_Car_SeatCapacity", "SeatCapacity IN (5, 7, 9, 13, 15, 17, 20, 26)");
        });

        builder.HasKey(c => c.CarId);

        builder.Property(c => c.Brand).IsRequired().HasMaxLength(60);
        builder.Property(c => c.ModelName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.RegistrationNumber).IsRequired().HasMaxLength(20);
        builder.Property(c => c.BaseCity).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasColumnType("text");
        builder.Property(c => c.StatusReason).HasMaxLength(500);
        builder.Property(c => c.FuelType).HasConversion<int>();
        // Concurrency token: an admin decision and a driver edit racing on the same row can't silently overwrite each other.
        builder.Property(c => c.Status).HasConversion<int>().IsConcurrencyToken();

        builder.Property(c => c.SubmittedForReviewAt).HasColumnType("datetime(6)");
        builder.Property(c => c.ApprovedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime(6)");

        // Stored normalised (upper case, no spaces), so "od 02 ab 1234" and "OD02AB1234" collide.
        builder.HasIndex(c => c.RegistrationNumber).IsUnique();
        builder.HasIndex(c => c.DriverId);
        builder.HasIndex(c => new { c.Status, c.BaseCity, c.SeatCapacity });

        builder.HasOne(c => c.Driver).WithMany(d => d.Cars).HasForeignKey(c => c.DriverId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Pricings).WithOne(p => p.Car).HasForeignKey(p => p.CarId).OnDelete(DeleteBehavior.Cascade);
        // The one-way pointer to the approved pricing; nulled (not cascaded) if that row ever goes.
        builder.HasOne(c => c.ActivePricing).WithMany().HasForeignKey(c => c.ActivePricingId).OnDelete(DeleteBehavior.SetNull);
    }
}
