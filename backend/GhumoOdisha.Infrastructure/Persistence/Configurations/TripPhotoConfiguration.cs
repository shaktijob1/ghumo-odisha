using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TripPhotoConfiguration : IEntityTypeConfiguration<TripPhoto>
{
    public void Configure(EntityTypeBuilder<TripPhoto> builder)
    {
        builder.ToTable("TripPhotos");

        builder.HasKey(p => p.TripPhotoId);

        builder.Property(p => p.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.TripId, p.DisplayOrder });
    }
}
