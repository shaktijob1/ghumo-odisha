using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarRentals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarAuditEvents",
                columns: table => new
                {
                    CarAuditEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    CarBookingId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValue = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValue = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorRole = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorId = table.Column<int>(type: "int", nullable: true),
                    IsVisibleToCustomer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarAuditEvents", x => x.CarAuditEventId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GoogleSubject = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DrivingLicenceNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LicenceExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExperienceYears = table.Column<int>(type: "int", nullable: true),
                    ProfilePhotoUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedForReviewAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverId);
                    table.CheckConstraint("CK_Driver_ExperienceYears", "ExperienceYears IS NULL OR (ExperienceYears >= 0 AND ExperienceYears <= 60)");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DriverRefreshTokens",
                columns: table => new
                {
                    DriverRefreshTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRefreshTokens", x => x.DriverRefreshTokenId);
                    table.ForeignKey(
                        name: "FK_DriverRefreshTokens_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CarBookings",
                columns: table => new
                {
                    CarBookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookingNumber = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    CarPricingId = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PickupCity = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PickupAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PickupAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DurationHours = table.Column<int>(type: "int", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EstimatedKm = table.Column<int>(type: "int", nullable: false),
                    EstimatedNights = table.Column<int>(type: "int", nullable: false),
                    EstimatedBaseFare = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedKmCharge = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedNightHaltCharge = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    BookingAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FinalKm = table.Column<int>(type: "int", nullable: true),
                    FinalNights = table.Column<int>(type: "int", nullable: true),
                    FinalBaseFare = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    FinalKmCharge = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    FinalNightHaltCharge = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AdditionalCharges = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AdditionalChargesNote = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    BalanceDue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    BalanceCollectedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    HoldExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RazorpayOrderId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RazorpayPaymentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    RefundMethod = table.Column<int>(type: "int", nullable: true),
                    RefundReference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RazorpayRefundId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RefundIssuedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RefundSettledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CustomerNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminNotes = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CancelledBy = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CancellationReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarBookings", x => x.CarBookingId);
                    table.CheckConstraint("CK_CarBooking_AdditionalCharges", "AdditionalCharges IS NULL OR AdditionalCharges >= 0");
                    table.CheckConstraint("CK_CarBooking_BookingAmount", "BookingAmount >= 0");
                    table.CheckConstraint("CK_CarBooking_Duration", "DurationHours > 0");
                    table.CheckConstraint("CK_CarBooking_EstimatedKm", "EstimatedKm >= 0");
                    table.CheckConstraint("CK_CarBooking_EstimatedNights", "EstimatedNights >= 0");
                    table.CheckConstraint("CK_CarBooking_FinalKm", "FinalKm IS NULL OR FinalKm >= 0");
                    table.CheckConstraint("CK_CarBooking_Window", "EndsAt > PickupAt");
                    table.ForeignKey(
                        name: "FK_CarBookings_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarBookings_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CarTripExecutions",
                columns: table => new
                {
                    CarTripExecutionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CarBookingId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartOdometerKm = table.Column<int>(type: "int", nullable: false),
                    StartLatitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    StartLongitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndOdometerKm = table.Column<int>(type: "int", nullable: true),
                    EndLatitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    EndLongitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    ActualKm = table.Column<int>(type: "int", nullable: true),
                    NightHalts = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarTripExecutions", x => x.CarTripExecutionId);
                    table.CheckConstraint("CK_CarTripExecution_ActualKm", "ActualKm IS NULL OR ActualKm = EndOdometerKm - StartOdometerKm");
                    table.CheckConstraint("CK_CarTripExecution_EndKm", "EndOdometerKm IS NULL OR EndOdometerKm >= StartOdometerKm");
                    table.CheckConstraint("CK_CarTripExecution_NightHalts", "NightHalts IS NULL OR NightHalts >= 0");
                    table.CheckConstraint("CK_CarTripExecution_StartKm", "StartOdometerKm >= 0");
                    table.ForeignKey(
                        name: "FK_CarTripExecutions_CarBookings_CarBookingId",
                        column: x => x.CarBookingId,
                        principalTable: "CarBookings",
                        principalColumn: "CarBookingId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CarPhotos",
                columns: table => new
                {
                    CarPhotoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarPhotos", x => x.CarPhotoId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CarPricings",
                columns: table => new
                {
                    CarPricingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CarId = table.Column<int>(type: "int", nullable: false),
                    MinimumKm = table.Column<int>(type: "int", nullable: false),
                    PricePerKm = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NightHaltPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedByRole = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedById = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarPricings", x => x.CarPricingId);
                    table.CheckConstraint("CK_CarPricing_MinimumKm", "MinimumKm >= 0");
                    table.CheckConstraint("CK_CarPricing_NightHaltPrice", "NightHaltPrice >= 0");
                    table.CheckConstraint("CK_CarPricing_PricePerKm", "PricePerKm >= 0");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CarPricingTiers",
                columns: table => new
                {
                    CarPricingTierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CarPricingId = table.Column<int>(type: "int", nullable: false),
                    UpToKm = table.Column<int>(type: "int", nullable: true),
                    BaseFare = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarPricingTiers", x => x.CarPricingTierId);
                    table.CheckConstraint("CK_CarPricingTier_BaseFare", "BaseFare >= 0");
                    table.CheckConstraint("CK_CarPricingTier_UpToKm", "UpToKm IS NULL OR UpToKm > 0");
                    table.ForeignKey(
                        name: "FK_CarPricingTiers_CarPricings_CarPricingId",
                        column: x => x.CarPricingId,
                        principalTable: "CarPricings",
                        principalColumn: "CarPricingId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cars",
                columns: table => new
                {
                    CarId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegistrationNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FuelType = table.Column<int>(type: "int", nullable: false),
                    SeatCapacity = table.Column<int>(type: "int", nullable: false),
                    HasAc = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseCity = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedForReviewAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    ActivePricingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cars", x => x.CarId);
                    table.CheckConstraint("CK_Car_SeatCapacity", "SeatCapacity IN (5, 7, 9, 13, 15, 17, 20, 26)");
                    table.ForeignKey(
                        name: "FK_Cars_CarPricings_ActivePricingId",
                        column: x => x.ActivePricingId,
                        principalTable: "CarPricings",
                        principalColumn: "CarPricingId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Cars_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DriverDocuments",
                columns: table => new
                {
                    DriverDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    CarId = table.Column<int>(type: "int", nullable: true),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverDocuments", x => x.DriverDocumentId);
                    table.ForeignKey(
                        name: "FK_DriverDocuments_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "CarId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DriverDocuments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CarAuditEvents_CarBookingId_CreatedAt",
                table: "CarAuditEvents",
                columns: new[] { "CarBookingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CarAuditEvents_EntityType_EntityId_CreatedAt",
                table: "CarAuditEvents",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_BookingNumber",
                table: "CarBookings",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_CarId_Status_PickupAt_EndsAt",
                table: "CarBookings",
                columns: new[] { "CarId", "Status", "PickupAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_CarPricingId",
                table: "CarBookings",
                column: "CarPricingId");

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_CreatedAt",
                table: "CarBookings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_CustomerId_ClientRequestId",
                table: "CarBookings",
                columns: new[] { "CustomerId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_DriverId_Status",
                table: "CarBookings",
                columns: new[] { "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_PaymentStatus",
                table: "CarBookings",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CarBookings_Status",
                table: "CarBookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CarPhotos_CarId_Kind_DisplayOrder",
                table: "CarPhotos",
                columns: new[] { "CarId", "Kind", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CarPricings_CarId_Status",
                table: "CarPricings",
                columns: new[] { "CarId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CarPricings_Status",
                table: "CarPricings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CarPricingTiers_CarPricingId_UpToKm",
                table: "CarPricingTiers",
                columns: new[] { "CarPricingId", "UpToKm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ActivePricingId",
                table: "Cars",
                column: "ActivePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_DriverId",
                table: "Cars",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_RegistrationNumber",
                table: "Cars",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cars_Status_BaseCity_SeatCapacity",
                table: "Cars",
                columns: new[] { "Status", "BaseCity", "SeatCapacity" });

            migrationBuilder.CreateIndex(
                name: "IX_CarTripExecutions_CarBookingId",
                table: "CarTripExecutions",
                column: "CarBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarTripExecutions_DriverId_CompletedAt",
                table: "CarTripExecutions",
                columns: new[] { "DriverId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverDocuments_CarId",
                table: "DriverDocuments",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverDocuments_DriverId_CarId",
                table: "DriverDocuments",
                columns: new[] { "DriverId", "CarId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverRefreshTokens_DriverId",
                table: "DriverRefreshTokens",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverRefreshTokens_TokenHash",
                table: "DriverRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Email",
                table: "Drivers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_GoogleSubject",
                table: "Drivers",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_PhoneNumber",
                table: "Drivers",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Status",
                table: "Drivers",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_CarBookings_CarPricings_CarPricingId",
                table: "CarBookings",
                column: "CarPricingId",
                principalTable: "CarPricings",
                principalColumn: "CarPricingId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CarBookings_Cars_CarId",
                table: "CarBookings",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "CarId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CarPhotos_Cars_CarId",
                table: "CarPhotos",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "CarId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CarPricings_Cars_CarId",
                table: "CarPricings",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "CarId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cars_CarPricings_ActivePricingId",
                table: "Cars");

            migrationBuilder.DropTable(
                name: "CarAuditEvents");

            migrationBuilder.DropTable(
                name: "CarPhotos");

            migrationBuilder.DropTable(
                name: "CarPricingTiers");

            migrationBuilder.DropTable(
                name: "CarTripExecutions");

            migrationBuilder.DropTable(
                name: "DriverDocuments");

            migrationBuilder.DropTable(
                name: "DriverRefreshTokens");

            migrationBuilder.DropTable(
                name: "CarBookings");

            migrationBuilder.DropTable(
                name: "CarPricings");

            migrationBuilder.DropTable(
                name: "Cars");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
