using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CarRoundTrip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DropToPickupKm",
                table: "CarBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RoundTrip",
                table: "CarBookings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropToPickupKm",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "RoundTrip",
                table: "CarBookings");
        }
    }
}
