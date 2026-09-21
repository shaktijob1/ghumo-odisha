using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TripDateSlotConfiguration : IEntityTypeConfiguration<TripDateSlot>
{
    public void Configure(EntityTypeBuilder<TripDateSlot> builder)
    {
        builder.ToTable("TripDateSlots", t =>
        {
            t.HasCheckConstraint("CK_TripDateSlot_TotalSeats", "TotalSeats > 0");
            t.HasCheckConstraint("CK_TripDateSlot_AvailableSeats_NonNegative", "AvailableSeats >= 0");
            t.HasCheckConstraint("CK_TripDateSlot_AvailableSeats_LteTotal", "AvailableSeats <= TotalSeats");
            t.HasCheckConstraint("CK_TripDateSlot_EndDate", "EndDate >= StartDate");
        });

        builder.HasKey(s => s.TripDateSlotId);

        builder.Property(s => s.StartDate).HasColumnType("date");
        builder.Property(s => s.EndDate).HasColumnType("date");

        builder.Property(s => s.Status)
            .HasConversion<int>();

        builder.Property(s => s.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(s => s.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(s => new { s.TripId, s.StartDate });
        builder.HasIndex(s => s.Status);

        builder.HasMany(s => s.Bookings)
            .WithOne(b => b.TripDateSlot)
            .HasForeignKey(b => b.TripDateSlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
