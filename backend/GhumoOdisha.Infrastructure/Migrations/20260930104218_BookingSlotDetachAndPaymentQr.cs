using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingSlotDetachAndPaymentQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TripDateSlotId",
                table: "Bookings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RemovedSlotEndDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RemovedSlotStartDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentQrCodes",
                columns: table => new
                {
                    PaymentQrCodeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Caption = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentQrCodes", x => x.PaymentQrCodeId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentQrCodes");

            migrationBuilder.DropColumn(
                name: "RemovedSlotEndDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RemovedSlotStartDate",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "TripDateSlotId",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
