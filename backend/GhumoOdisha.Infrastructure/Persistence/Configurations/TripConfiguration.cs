using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips", t => t.HasCheckConstraint("CK_Trip_AmountPerPerson", "AmountPerPerson >= 0"));

        builder.HasKey(t => t.TripId);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(t => t.AmountPerPerson)
            .HasColumnType("decimal(10,2)");

        builder.Property(t => t.Status)
            .HasConversion<int>();

        builder.Property(t => t.ItineraryPdfUrl)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(t => t.Status);

        builder.HasMany(t => t.TripPhotos)
            .WithOne(p => p.Trip)
            .HasForeignKey(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.TripHighlights)
            .WithOne(h => h.Trip)
            .HasForeignKey(h => h.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.RoomPhotos)
            .WithOne(r => r.Trip)
            .HasForeignKey(r => r.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ItineraryDays)
            .WithOne(d => d.Trip)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.TripDateSlots)
            .WithOne(s => s.Trip)
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Bookings)
            .WithOne(b => b.Trip)
            .HasForeignKey(b => b.TripId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
