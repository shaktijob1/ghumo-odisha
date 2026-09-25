using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripsHeroAndDestinationCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Page",
                table: "SiteHeroPhotos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Destinations",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SiteHeroPhotos_Page",
                table: "SiteHeroPhotos",
                column: "Page",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiteHeroPhotos_Page",
                table: "SiteHeroPhotos");

            migrationBuilder.DropColumn(
                name: "Page",
                table: "SiteHeroPhotos");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Destinations");
        }
    }
}
