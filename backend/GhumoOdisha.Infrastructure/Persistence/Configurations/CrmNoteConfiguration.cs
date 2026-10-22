using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CrmNoteConfiguration : IEntityTypeConfiguration<CrmNote>
{
    public void Configure(EntityTypeBuilder<CrmNote> builder)
    {
        builder.ToTable("CrmNotes");

        builder.HasKey(n => n.CrmNoteId);

        builder.Property(n => n.Text).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(n => new { n.CustomerId, n.CreatedAt });

        builder.HasOne(n => n.Customer).WithMany(c => c.CrmNotes).HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.TripInterest).WithMany().HasForeignKey(n => n.CustomerTripInterestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(n => n.AdminUser).WithMany().HasForeignKey(n => n.AdminUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
