using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CarPickupDropKm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DestinationLocation",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "DropLegKm",
                table: "CarBookings");

            migrationBuilder.RenameColumn(
                name: "OneWayKm",
                table: "CarBookings",
                newName: "PickupToDropKm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PickupToDropKm",
                table: "CarBookings",
                newName: "OneWayKm");

            migrationBuilder.AddColumn<double>(
                name: "DestinationLatitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationLocation",
                table: "CarBookings",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "DestinationLongitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DropLegKm",
                table: "CarBookings",
                type: "int",
                nullable: true);
        }
    }
}
