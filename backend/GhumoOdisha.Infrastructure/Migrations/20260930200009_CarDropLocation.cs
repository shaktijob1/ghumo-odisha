using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CarDropLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DropLatitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DropLegKm",
                table: "CarBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropLocation",
                table: "CarBookings",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "DropLongitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnToBaseKm",
                table: "CarBookings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropLatitude",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DropLegKm",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DropLocation",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DropLongitude",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "ReturnToBaseKm",
                table: "CarBookings");
        }
    }
}
