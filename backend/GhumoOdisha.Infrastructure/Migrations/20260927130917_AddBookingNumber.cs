using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingNumber",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing bookings get 6-digit numbers before the unique index is added. 611953 shares no
            // factor with 900000, so BookingId * 611953 mod 900000 is different for every booking
            // (no collisions) while not looking sequential. New bookings get random numbers (BookingNumbers).
            migrationBuilder.Sql("UPDATE Bookings SET BookingNumber = 100000 + MOD(BookingId * 611953, 900000);");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingNumber",
                table: "Bookings",
                column: "BookingNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingNumber",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BookingNumber",
                table: "Bookings");
        }
    }
}
