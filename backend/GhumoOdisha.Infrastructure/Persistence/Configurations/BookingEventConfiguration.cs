using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BookingEventConfiguration : IEntityTypeConfiguration<BookingEvent>
{
    public void Configure(EntityTypeBuilder<BookingEvent> builder)
    {
        builder.ToTable("BookingEvents");

        builder.HasKey(e => e.BookingEventId);

        builder.Property(e => e.EventType).HasConversion<int>();
        builder.Property(e => e.Title).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(600);
        builder.Property(e => e.Actor).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.BookingId, e.CreatedAt });

        builder.HasOne(e => e.Booking).WithMany(b => b.Events).HasForeignKey(e => e.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
