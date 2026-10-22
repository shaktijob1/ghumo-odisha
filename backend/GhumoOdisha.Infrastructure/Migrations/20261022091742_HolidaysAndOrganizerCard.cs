using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HolidaysAndOrganizerCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "OrganizerPhotos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "OrganizerPhotos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HelpText",
                table: "OrganizerPhotos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HelpTitle",
                table: "OrganizerPhotos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "OrganizerPhotos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "OrganizerPhotos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "OrganizerPhotos",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhotoCaption",
                table: "OrganizerPhotos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Quote",
                table: "OrganizerPhotos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "OrganizerPhotos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HolidayDestinations",
                columns: table => new
                {
                    HolidayDestinationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tagline = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CoverImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayDestinations", x => x.HolidayDestinationId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HolidayPackages",
                columns: table => new
                {
                    HolidayPackageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Summary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Days = table.Column<int>(type: "int", nullable: false),
                    Nights = table.Column<int>(type: "int", nullable: false),
                    PriceFrom = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Highlights = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CoverImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HolidayDestinationId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayPackages", x => x.HolidayPackageId);
                    table.CheckConstraint("CK_HolidayPackage_Duration", "Days >= 1 AND Nights >= 0");
                    table.CheckConstraint("CK_HolidayPackage_PriceFrom", "PriceFrom IS NULL OR PriceFrom >= 0");
                    table.ForeignKey(
                        name: "FK_HolidayPackages_HolidayDestinations_HolidayDestinationId",
                        column: x => x.HolidayDestinationId,
                        principalTable: "HolidayDestinations",
                        principalColumn: "HolidayDestinationId",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayDestinations_IsActive_DisplayOrder",
                table: "HolidayDestinations",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HolidayDestinations_Name",
                table: "HolidayDestinations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayPackages_HolidayDestinationId",
                table: "HolidayPackages",
                column: "HolidayDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayPackages_IsActive_DisplayOrder",
                table: "HolidayPackages",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HolidayPackages");

            migrationBuilder.DropTable(
                name: "HolidayDestinations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "HelpText",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "HelpTitle",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "PhotoCaption",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "Quote",
                table: "OrganizerPhotos");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "OrganizerPhotos");

            migrationBuilder.UpdateData(
                table: "OrganizerPhotos",
                keyColumn: "ImageUrl",
                keyValue: null,
                column: "ImageUrl",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "OrganizerPhotos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
