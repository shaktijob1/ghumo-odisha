using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class PickupPointConfiguration : IEntityTypeConfiguration<PickupPoint>
{
    public void Configure(EntityTypeBuilder<PickupPoint> builder)
    {
        builder.ToTable("PickupPoints");

        builder.HasKey(p => p.PickupPointId);

        builder.Property(p => p.Location)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Time)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.TripId, p.DisplayOrder });
    }
}
