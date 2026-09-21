using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class VehiclePhotoConfiguration : IEntityTypeConfiguration<VehiclePhoto>
{
    public void Configure(EntityTypeBuilder<VehiclePhoto> builder)
    {
        builder.ToTable("VehiclePhotos");

        builder.HasKey(v => v.VehiclePhotoId);

        builder.Property(v => v.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(v => new { v.TripId, v.DisplayOrder });
    }
}
