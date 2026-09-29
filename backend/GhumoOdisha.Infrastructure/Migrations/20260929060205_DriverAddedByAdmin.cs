using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DriverAddedByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AddedByAdminId",
                table: "Drivers",
                type: "int",
                nullable: true);

            // Drivers already added by an admin (recorded in the Cars history) get the flag too.
            migrationBuilder.Sql(@"
UPDATE Drivers d
JOIN CarAuditEvents e ON e.EntityType = 0 AND e.EntityId = d.DriverId AND e.Action = 'DriverCreated'
SET d.AddedByAdminId = e.ActorId
WHERE d.AddedByAdminId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedByAdminId",
                table: "Drivers");
        }
    }
}
