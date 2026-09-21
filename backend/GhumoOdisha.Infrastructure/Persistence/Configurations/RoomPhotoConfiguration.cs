using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class RoomPhotoConfiguration : IEntityTypeConfiguration<RoomPhoto>
{
    public void Configure(EntityTypeBuilder<RoomPhoto> builder)
    {
        builder.ToTable("RoomPhotos");

        builder.HasKey(r => r.RoomPhotoId);

        builder.Property(r => r.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(r => new { r.TripId, r.DisplayOrder });
    }
}
