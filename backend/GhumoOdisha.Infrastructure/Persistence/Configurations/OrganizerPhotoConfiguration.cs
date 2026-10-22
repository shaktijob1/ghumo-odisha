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

        // Optional: the row can hold the card's texts before any photo is uploaded.
        builder.Property(o => o.ImageUrl).HasMaxLength(500);
        builder.Property(o => o.Name).HasMaxLength(100);
        builder.Property(o => o.Role).HasMaxLength(100);
        builder.Property(o => o.Phone).HasMaxLength(20);
        builder.Property(o => o.Email).HasMaxLength(200);
        builder.Property(o => o.InstagramUrl).HasMaxLength(300);
        builder.Property(o => o.Quote).HasMaxLength(300);
        builder.Property(o => o.PhotoCaption).HasMaxLength(100);
        builder.Property(o => o.HelpTitle).HasMaxLength(100);
        builder.Property(o => o.HelpText).HasMaxLength(200);

        builder.Property(o => o.UpdatedAt).HasColumnType("datetime(6)");
    }
}
