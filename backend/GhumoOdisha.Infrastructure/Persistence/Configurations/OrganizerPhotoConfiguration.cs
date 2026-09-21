using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class OrganizerPhotoConfiguration : IEntityTypeConfiguration<OrganizerPhoto>
{
    public void Configure(EntityTypeBuilder<OrganizerPhoto> builder)
    {
        builder.ToTable("OrganizerPhotos");

        builder.HasKey(o => o.OrganizerPhotoId);

        builder.Property(o => o.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.UpdatedAt).HasColumnType("datetime(6)");
    }
}
