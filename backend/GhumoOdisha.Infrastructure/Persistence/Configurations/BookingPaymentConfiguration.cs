using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingPaymentConfiguration : IEntityTypeConfiguration<BookingPayment>
{
    public void Configure(EntityTypeBuilder<BookingPayment> builder)
    {
        builder.ToTable("BookingPayments", t =>
        {
            t.HasCheckConstraint("CK_BookingPayment_Amount_Positive", "Amount > 0");
        });

        builder.HasKey(p => p.BookingPaymentId);

        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        builder.Property(p => p.Method).HasConversion<int>();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.Property(p => p.RecordedBy).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PaidAt).HasColumnType("datetime(6)");
        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => new { p.BookingId, p.PaidAt });

        builder.HasOne(p => p.Booking).WithMany(b => b.Payments).HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
