using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingStatusTimelineEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bookings put into a final status without going through the service (direct DB edits,
            // an old test shortcut) have no cancelled date and no timeline step for it, so the
            // timeline stopped at "Confirmed" while the status said "Cancelled". Repair them.
            // Every statement only adds what's missing, so it is safe on data that's already right.
            migrationBuilder.Sql(@"
UPDATE Bookings
   SET CancelledAt = UpdatedAt
 WHERE BookingStatus = 4 AND CancelledAt IS NULL;");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT b.BookingId, 3, 'Booking cancelled', NULL, 'System', 1, b.CancelledAt
  FROM Bookings b
 WHERE b.BookingStatus = 4
   AND NOT EXISTS (SELECT 1 FROM BookingEvents e WHERE e.BookingId = b.BookingId AND e.EventType = 3);");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT b.BookingId, 2, 'Booking request declined', NULL, 'Admin', 1, b.UpdatedAt
  FROM Bookings b
 WHERE b.BookingStatus = 3
   AND NOT EXISTS (SELECT 1 FROM BookingEvents e WHERE e.BookingId = b.BookingId AND e.EventType = 2);");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT b.BookingId, 4, 'Trip completed', NULL, 'System', 1, b.UpdatedAt
  FROM Bookings b
 WHERE b.BookingStatus = 5
   AND NOT EXISTS (SELECT 1 FROM BookingEvents e WHERE e.BookingId = b.BookingId AND e.EventType = 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
