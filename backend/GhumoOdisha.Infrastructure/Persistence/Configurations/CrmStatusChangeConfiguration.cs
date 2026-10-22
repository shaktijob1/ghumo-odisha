using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CrmStatusChangeConfiguration : IEntityTypeConfiguration<CrmStatusChange>
{
    public void Configure(EntityTypeBuilder<CrmStatusChange> builder)
    {
        builder.ToTable("CrmStatusChanges");

        builder.HasKey(s => s.CrmStatusChangeId);

        builder.Property(s => s.FromStatus).HasConversion<int?>();
        builder.Property(s => s.ToStatus).HasConversion<int>();
        builder.Property(s => s.Reason).HasMaxLength(500);
        builder.Property(s => s.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(s => new { s.CustomerId, s.CreatedAt });
        // Monthly reports count stage changes (bookings, cancellations) by date.
        builder.HasIndex(s => new { s.ToStatus, s.CreatedAt });

        builder.HasOne(s => s.Customer).WithMany(c => c.CrmStatusChanges).HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.TripInterest).WithMany().HasForeignKey(s => s.CustomerTripInterestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.AdminUser).WithMany().HasForeignKey(s => s.AdminUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
