using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentsAndDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Bookings",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BookingPayments",
                columns: table => new
                {
                    BookingPaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordedBy = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPayments", x => x.BookingPaymentId);
                    table.CheckConstraint("CK_BookingPayment_Amount_Positive", "Amount > 0");
                    table.ForeignKey(
                        name: "FK_BookingPayments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BookingPayments_BookingId_PaidAt",
                table: "BookingPayments",
                columns: new[] { "BookingId", "PaidAt" });

            // Backfill: confirmed bookings priced their discount into TotalAmount without storing it.
            migrationBuilder.Sql(@"
UPDATE Bookings
   SET DiscountAmount = GREATEST(0, AmountPerPerson * NumberOfSeats - TotalAmount)
 WHERE ConfirmedAt IS NOT NULL;");

            // Backfill: every amount already paid becomes the booking's first payment row, so
            // AdvanceAmount = SUM(BookingPayments.Amount) holds for existing bookings too.
            migrationBuilder.Sql(@"
INSERT INTO BookingPayments (BookingId, Amount, Method, Reference, Notes, RecordedBy, PaidAt, CreatedAt)
SELECT BookingId,
       AdvanceAmount,
       CASE WHEN RazorpayPaymentId IS NOT NULL AND RazorpayPaymentId <> '' THEN 0 ELSE 1 END,
       NULLIF(RazorpayPaymentId, ''),
       'Recorded before payment history was introduced',
       CASE WHEN RazorpayPaymentId IS NOT NULL AND RazorpayPaymentId <> '' THEN 'Customer' ELSE 'Admin' END,
       COALESCE(ConfirmedAt, UpdatedAt),
       UTC_TIMESTAMP(6)
  FROM Bookings
 WHERE AdvanceAmount > 0 AND ConfirmedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingPayments");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Bookings");
        }
    }
}
