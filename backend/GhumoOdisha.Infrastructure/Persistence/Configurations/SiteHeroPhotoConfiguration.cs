using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class SiteHeroPhotoConfiguration : IEntityTypeConfiguration<SiteHeroPhoto>
{
    public void Configure(EntityTypeBuilder<SiteHeroPhoto> builder)
    {
        builder.ToTable("SiteHeroPhotos");

        builder.HasKey(h => h.SiteHeroPhotoId);

        builder.Property(h => h.Page).HasConversion<int>();
        builder.HasIndex(h => h.Page).IsUnique();

        builder.Property(h => h.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.UpdatedAt).HasColumnType("datetime(6)");
    }
}
