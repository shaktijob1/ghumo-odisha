using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingRefundConfiguration : IEntityTypeConfiguration<BookingRefund>
{
    public void Configure(EntityTypeBuilder<BookingRefund> builder)
    {
        builder.ToTable("BookingRefunds", t =>
        {
            t.HasCheckConstraint("CK_BookingRefunds_Amount", "Amount > 0 AND Amount <= AmountPaid");
        });

        builder.HasKey(r => r.BookingRefundId);

        builder.Property(r => r.AmountPaid).HasColumnType("decimal(10,2)");
        builder.Property(r => r.Amount).HasColumnType("decimal(10,2)");
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.Method).HasConversion<int?>();
        builder.Property(r => r.Reference).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.Property(r => r.RequestedBy).IsRequired().HasMaxLength(20);

        builder.Property(r => r.RequestedAt).HasColumnType("datetime(6)");
        builder.Property(r => r.InitiatedAt).HasColumnType("datetime(6)");
        builder.Property(r => r.SettledAt).HasColumnType("datetime(6)");
        builder.Property(r => r.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(r => r.UpdatedAt).HasColumnType("datetime(6)");

        // One refund per booking — a repeat cancel can never queue a second refund.
        builder.HasIndex(r => r.BookingId).IsUnique();
        builder.HasIndex(r => new { r.Status, r.RequestedAt });

        builder.HasOne(r => r.Booking)
            .WithOne(b => b.Refund)
            .HasForeignKey<BookingRefund>(r => r.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
