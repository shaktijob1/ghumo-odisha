using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarPhotoConfiguration : IEntityTypeConfiguration<CarPhoto>
{
    public void Configure(EntityTypeBuilder<CarPhoto> builder)
    {
        builder.ToTable("CarPhotos");

        builder.HasKey(p => p.CarPhotoId);

        builder.Property(p => p.Kind).HasConversion<int>();
        builder.Property(p => p.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.CarId, p.Kind, p.DisplayOrder });

        builder.HasOne(p => p.Car).WithMany(c => c.Photos).HasForeignKey(p => p.CarId).OnDelete(DeleteBehavior.Cascade);
    }
}
