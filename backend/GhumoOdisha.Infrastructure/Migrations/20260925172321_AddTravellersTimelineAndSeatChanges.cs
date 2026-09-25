using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTravellersTimelineAndSeatChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_AdvanceAmount_LteTotal",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_RemainingAmount",
                table: "Bookings");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Bookings",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "FemaleCount",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaleCount",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefundWaived",
                table: "Bookings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BookingEvents",
                columns: table => new
                {
                    BookingEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Actor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsVisibleToCustomer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingEvents", x => x.BookingEventId);
                    table.ForeignKey(
                        name: "FK_BookingEvents_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BookingTravellers",
                columns: table => new
                {
                    BookingTravellerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    AadhaarLast4 = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkedCustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTravellers", x => x.BookingTravellerId);
                    table.CheckConstraint("CK_BookingTraveller_AadhaarLast4", "AadhaarLast4 IS NULL OR CHAR_LENGTH(AadhaarLast4) = 4");
                    table.CheckConstraint("CK_BookingTraveller_SeatNumber", "SeatNumber > 0");
                    table.ForeignKey(
                        name: "FK_BookingTravellers_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingTravellers_Customers_LinkedCustomerId",
                        column: x => x.LinkedCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_GenderCounts",
                table: "Bookings",
                sql: "COALESCE(MaleCount, 0) + COALESCE(FemaleCount, 0) <= NumberOfSeats");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_RemainingAmount",
                table: "Bookings",
                sql: "RemainingAmount = GREATEST(TotalAmount - AdvanceAmount, 0)");

            migrationBuilder.CreateIndex(
                name: "IX_BookingEvents_BookingId_CreatedAt",
                table: "BookingEvents",
                columns: new[] { "BookingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingTravellers_BookingId_SeatNumber",
                table: "BookingTravellers",
                columns: new[] { "BookingId", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingTravellers_LinkedCustomerId",
                table: "BookingTravellers",
                column: "LinkedCustomerId");

            // Backfill a starting timeline for existing bookings from the timestamps already on file.
            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT BookingId,
       CASE WHEN BookingSource = 0 THEN 0 ELSE 10 END,
       CASE WHEN BookingSource = 0 THEN 'Booking requested' ELSE 'Booking created by Ghumo Odisha' END,
       CONCAT(NumberOfSeats, ' seat(s)'),
       CASE WHEN BookingSource = 0 THEN 'Customer' ELSE 'Admin' END,
       1,
       RequestedAt
  FROM Bookings;");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT BookingId, 1, 'Booking confirmed',
       CONCAT(NumberOfSeats, ' seat(s) reserved', CASE WHEN AdvanceAmount > 0 THEN CONCAT(' · ₹', FORMAT(AdvanceAmount, 0), ' paid') ELSE '' END),
       CASE WHEN RazorpayPaymentId IS NOT NULL AND RazorpayPaymentId <> '' THEN 'Customer' ELSE 'Admin' END,
       1, ConfirmedAt
  FROM Bookings
 WHERE ConfirmedAt IS NOT NULL;");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT BookingId, 3, 'Booking cancelled', NULL, 'System', 1, CancelledAt
  FROM Bookings
 WHERE BookingStatus = 4 AND CancelledAt IS NOT NULL;");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT BookingId, 2, 'Booking request declined', NULL, 'Admin', 1, UpdatedAt
  FROM Bookings
 WHERE BookingStatus = 3;");

            migrationBuilder.Sql(@"
INSERT INTO BookingEvents (BookingId, EventType, Title, Description, Actor, IsVisibleToCustomer, CreatedAt)
SELECT BookingId, 4, 'Trip completed', NULL, 'System', 1, UpdatedAt
  FROM Bookings
 WHERE BookingStatus = 5;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingEvents");

            migrationBuilder.DropTable(
                name: "BookingTravellers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_GenderCounts",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_RemainingAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FemaleCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MaleCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RefundWaived",
                table: "Bookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_AdvanceAmount_LteTotal",
                table: "Bookings",
                sql: "AdvanceAmount <= TotalAmount");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_RemainingAmount",
                table: "Bookings",
                sql: "RemainingAmount = TotalAmount - AdvanceAmount");
        }
    }
}
