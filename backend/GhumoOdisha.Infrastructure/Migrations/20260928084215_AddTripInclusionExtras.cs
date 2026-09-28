using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripInclusionExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludesAcVehicle",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesBonfire",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesCamping",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesMusicalNight",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesPushbackVehicle",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesSwimmingPool",
                table: "Trips",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludesAcVehicle",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IncludesBonfire",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IncludesCamping",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IncludesMusicalNight",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IncludesPushbackVehicle",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IncludesSwimmingPool",
                table: "Trips");
        }
    }
}
