using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponHolderAndCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "CouponRedemptions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewCustomer",
                table: "CouponRedemptions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfSeats",
                table: "CouponRedemptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPerSeat",
                table: "CouponCodes",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 200m);

            migrationBuilder.AddColumn<string>(
                name: "HolderName",
                table: "CouponCodes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CouponCode_CommissionPerSeat_NonNegative",
                table: "CouponCodes",
                sql: "CommissionPerSeat >= 0");

            // Backfill coupons and redemptions that existed before this migration.
            migrationBuilder.Sql("UPDATE CouponCodes SET HolderName = Code WHERE HolderName = '';");
            migrationBuilder.Sql(@"
                UPDATE CouponRedemptions r
                  JOIN Bookings b ON b.BookingId = r.BookingId
                  JOIN CouponCodes c ON c.CouponCodeId = r.CouponCodeId
                   SET r.NumberOfSeats = b.NumberOfSeats,
                       r.CommissionAmount = c.CommissionPerSeat * b.NumberOfSeats,
                       r.IsNewCustomer = NOT EXISTS (
                           SELECT 1 FROM (SELECT BookingId, CustomerId, ConfirmedAt FROM Bookings) prior
                            WHERE prior.CustomerId = r.CustomerId
                              AND prior.BookingId <> r.BookingId
                              AND prior.ConfirmedAt IS NOT NULL
                              AND prior.ConfirmedAt < r.RedeemedAt);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CouponCode_CommissionPerSeat_NonNegative",
                table: "CouponCodes");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "CouponRedemptions");

            migrationBuilder.DropColumn(
                name: "IsNewCustomer",
                table: "CouponRedemptions");

            migrationBuilder.DropColumn(
                name: "NumberOfSeats",
                table: "CouponRedemptions");

            migrationBuilder.DropColumn(
                name: "CommissionPerSeat",
                table: "CouponCodes");

            migrationBuilder.DropColumn(
                name: "HolderName",
                table: "CouponCodes");
        }
    }
}
