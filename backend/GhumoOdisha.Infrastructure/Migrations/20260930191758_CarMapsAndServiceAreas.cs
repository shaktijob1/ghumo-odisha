using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CarMapsAndServiceAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BaseLatitude",
                table: "Drivers",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseLocationLabel",
                table: "Drivers",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "BaseLongitude",
                table: "Drivers",
                type: "double",
                nullable: true);

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
                name: "DriverApproachKm",
                table: "CarBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OneWayKm",
                table: "CarBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLatitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "CarBookings",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "PickupLongitude",
                table: "CarBookings",
                type: "double",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceAreas",
                columns: table => new
                {
                    ServiceAreaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BoundaryJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pincodes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAreas", x => x.ServiceAreaId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAreas_IsActive",
                table: "ServiceAreas",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAreas");

            migrationBuilder.DropColumn(
                name: "BaseLatitude",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "BaseLocationLabel",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "BaseLongitude",
                table: "Drivers");

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
                name: "DriverApproachKm",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "OneWayKm",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "CarBookings");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "CarBookings");
        }
    }
}
