using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarTripExecutionConfiguration : IEntityTypeConfiguration<CarTripExecution>
{
    public void Configure(EntityTypeBuilder<CarTripExecution> builder)
    {
        builder.ToTable("CarTripExecutions", t =>
        {
            t.HasCheckConstraint("CK_CarTripExecution_StartKm", "StartOdometerKm >= 0");
            // The database itself refuses an end reading below the start reading.
            t.HasCheckConstraint("CK_CarTripExecution_EndKm", "EndOdometerKm IS NULL OR EndOdometerKm >= StartOdometerKm");
            t.HasCheckConstraint("CK_CarTripExecution_ActualKm", "ActualKm IS NULL OR ActualKm = EndOdometerKm - StartOdometerKm");
            t.HasCheckConstraint("CK_CarTripExecution_NightHalts", "NightHalts IS NULL OR NightHalts >= 0");
        });

        builder.HasKey(e => e.CarTripExecutionId);

        builder.Property(e => e.StartLatitude).HasColumnType("decimal(9,6)");
        builder.Property(e => e.StartLongitude).HasColumnType("decimal(9,6)");
        builder.Property(e => e.EndLatitude).HasColumnType("decimal(9,6)");
        builder.Property(e => e.EndLongitude).HasColumnType("decimal(9,6)");
        builder.Property(e => e.StartedAt).HasColumnType("datetime(6)");
        builder.Property(e => e.EndedAt).HasColumnType("datetime(6)");
        builder.Property(e => e.CompletedAt).HasColumnType("datetime(6)");

        // One execution per booking — a second Start Trip can't insert a second row.
        builder.HasIndex(e => e.CarBookingId).IsUnique();
        builder.HasIndex(e => new { e.DriverId, e.CompletedAt });

        builder.HasOne(e => e.CarBooking).WithOne(b => b.Execution).HasForeignKey<CarTripExecution>(e => e.CarBookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
