using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TripHighlightConfiguration : IEntityTypeConfiguration<TripHighlight>
{
    public void Configure(EntityTypeBuilder<TripHighlight> builder)
    {
        builder.ToTable("TripHighlights");

        builder.HasKey(h => h.TripHighlightId);

        builder.Property(h => h.PlaceName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Description)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(h => h.PhotoUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(h => new { h.TripId, h.DisplayOrder });
    }
}
