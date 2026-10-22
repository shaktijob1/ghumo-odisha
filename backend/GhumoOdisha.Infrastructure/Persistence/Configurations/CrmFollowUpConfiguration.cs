using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CrmFollowUpConfiguration : IEntityTypeConfiguration<CrmFollowUp>
{
    public void Configure(EntityTypeBuilder<CrmFollowUp> builder)
    {
        builder.ToTable("CrmFollowUps");

        builder.HasKey(f => f.CrmFollowUpId);

        builder.Property(f => f.DueDate).HasColumnType("date");
        builder.Property(f => f.Status).HasConversion<int>();
        builder.Property(f => f.Note).HasMaxLength(1000);
        builder.Property(f => f.Outcome).HasMaxLength(1000);
        builder.Property(f => f.CompletedAt).HasColumnType("datetime(6)");
        builder.Property(f => f.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(f => f.UpdatedAt).HasColumnType("datetime(6)");

        // Today / overdue / upcoming lists all filter on pending + due date.
        builder.HasIndex(f => new { f.Status, f.DueDate });
        builder.HasIndex(f => f.CustomerId);
        builder.HasIndex(f => f.CustomerTripInterestId);

        builder.HasOne(f => f.Customer).WithMany(c => c.CrmFollowUps).HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(f => f.TripInterest).WithMany().HasForeignKey(f => f.CustomerTripInterestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(f => f.AssignedAdminUser).WithMany().HasForeignKey(f => f.AssignedAdminUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(f => f.CreatedByAdminUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(f => f.CompletedByAdminUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
