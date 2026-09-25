using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.ToTable("Destinations");

        builder.HasKey(d => d.DestinationId);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Tagline).HasMaxLength(300);
        builder.Property(d => d.Region).HasMaxLength(200);
        builder.Property(d => d.HeroImageUrl).HasMaxLength(500);
        builder.Property(d => d.CoverImageUrl).HasMaxLength(500);
        builder.Property(d => d.AboutText).HasColumnType("text");
        builder.Property(d => d.BestSeason).HasMaxLength(100);
        builder.Property(d => d.DistanceFromBhubaneswar).HasMaxLength(100);
        builder.Property(d => d.IdealDuration).HasMaxLength(100);
        builder.Property(d => d.KnownFor).HasMaxLength(200);

        builder.Property(d => d.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(d => d.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(d => d.Slug).IsUnique();

        builder.HasMany(d => d.Trips)
            .WithMany(t => t.Destinations)
            .UsingEntity(j => j.ToTable("TripDestinations"));
    }
}
