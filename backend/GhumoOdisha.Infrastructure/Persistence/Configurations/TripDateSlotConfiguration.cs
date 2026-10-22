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
            t.HasCheckConstraint("CK_TripDateSlot_MinimumBookedSeats", "MinimumBookedSeats IS NULL OR (MinimumBookedSeats > 0 AND MinimumBookedSeats <= TotalSeats)");
            t.HasCheckConstraint("CK_TripDateSlot_PricePerPerson", "PricePerPerson IS NULL OR PricePerPerson > 0");
            t.HasCheckConstraint("CK_TripDateSlot_EarlyBird", "(EarlyBirdPrice IS NULL AND EarlyBirdEndsOn IS NULL) OR (EarlyBirdPrice > 0 AND EarlyBirdEndsOn IS NOT NULL)");
        });

        builder.HasKey(s => s.TripDateSlotId);

        builder.Property(s => s.StartDate).HasColumnType("date");
        builder.Property(s => s.EndDate).HasColumnType("date");

        builder.Property(s => s.Status)
            .HasConversion<int>();

        builder.Property(s => s.Vehicle).HasMaxLength(100);
        builder.Property(s => s.PricePerPerson).HasColumnType("decimal(10,2)");
        builder.Property(s => s.EarlyBirdPrice).HasColumnType("decimal(10,2)");
        builder.Property(s => s.EarlyBirdEndsOn).HasColumnType("date");

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
