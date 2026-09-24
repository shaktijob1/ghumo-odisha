using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TermsAcceptanceConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
    {
        builder.ToTable("TermsAcceptances");

        builder.HasKey(t => t.TermsAcceptanceId);

        builder.Property(t => t.Version).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Text).IsRequired().HasColumnType("text");
        builder.Property(t => t.AcceptedAt).HasColumnType("datetime(6)");

        builder.HasIndex(t => t.BookingId);
        builder.HasIndex(t => t.CustomerId);

        builder.HasOne(t => t.Customer)
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Booking)
            .WithMany()
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
