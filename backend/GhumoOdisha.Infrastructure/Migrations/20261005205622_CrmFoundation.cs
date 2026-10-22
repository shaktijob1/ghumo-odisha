using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrmFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EarlyBirdEndsOn",
                table: "TripDateSlots",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EarlyBirdPrice",
                table: "TripDateSlots",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumBookedSeats",
                table: "TripDateSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerPerson",
                table: "TripDateSlots",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vehicle",
                table: "TripDateSlots",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AlternatePhoneNumber",
                table: "Customers",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AssignedAdminUserId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "CrmStatus",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CrmSummary",
                table: "Customers",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContactAt",
                table: "Customers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadSource",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 3); // existing customers signed up on the website (LeadSource.Website)

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppAvailable",
                table: "Customers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CrmWhatsAppTemplates",
                columns: table => new
                {
                    CrmWhatsAppTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmWhatsAppTemplates", x => x.CrmWhatsAppTemplateId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomerTripInterests",
                columns: table => new
                {
                    CustomerTripInterestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    TripDateSlotId = table.Column<int>(type: "int", nullable: true),
                    Travellers = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InterestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastContactAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    LostReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByAdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTripInterests", x => x.CustomerTripInterestId);
                    table.CheckConstraint("CK_CustomerTripInterest_ExpectedAmount", "ExpectedAmount IS NULL OR ExpectedAmount >= 0");
                    table.CheckConstraint("CK_CustomerTripInterest_Travellers", "Travellers > 0");
                    table.ForeignKey(
                        name: "FK_CustomerTripInterests_AdminUsers_CreatedByAdminUserId",
                        column: x => x.CreatedByAdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerTripInterests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerTripInterests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerTripInterests_TripDateSlots_TripDateSlotId",
                        column: x => x.TripDateSlotId,
                        principalTable: "TripDateSlots",
                        principalColumn: "TripDateSlotId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerTripInterests_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CrmCommunications",
                columns: table => new
                {
                    CrmCommunicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerTripInterestId = table.Column<int>(type: "int", nullable: true),
                    CrmWhatsAppTemplateId = table.Column<int>(type: "int", nullable: true),
                    TemplateName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessagePreview = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmCommunications", x => x.CrmCommunicationId);
                    table.ForeignKey(
                        name: "FK_CrmCommunications_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmCommunications_CrmWhatsAppTemplates_CrmWhatsAppTemplateId",
                        column: x => x.CrmWhatsAppTemplateId,
                        principalTable: "CrmWhatsAppTemplates",
                        principalColumn: "CrmWhatsAppTemplateId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmCommunications_CustomerTripInterests_CustomerTripInterest~",
                        column: x => x.CustomerTripInterestId,
                        principalTable: "CustomerTripInterests",
                        principalColumn: "CustomerTripInterestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmCommunications_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CrmFollowUps",
                columns: table => new
                {
                    CrmFollowUpId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerTripInterestId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByAdminUserId = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedByAdminUserId = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmFollowUps", x => x.CrmFollowUpId);
                    table.ForeignKey(
                        name: "FK_CrmFollowUps_AdminUsers_AssignedAdminUserId",
                        column: x => x.AssignedAdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmFollowUps_AdminUsers_CompletedByAdminUserId",
                        column: x => x.CompletedByAdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmFollowUps_AdminUsers_CreatedByAdminUserId",
                        column: x => x.CreatedByAdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmFollowUps_CustomerTripInterests_CustomerTripInterestId",
                        column: x => x.CustomerTripInterestId,
                        principalTable: "CustomerTripInterests",
                        principalColumn: "CustomerTripInterestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmFollowUps_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CrmNotes",
                columns: table => new
                {
                    CrmNoteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerTripInterestId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmNotes", x => x.CrmNoteId);
                    table.ForeignKey(
                        name: "FK_CrmNotes_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmNotes_CustomerTripInterests_CustomerTripInterestId",
                        column: x => x.CustomerTripInterestId,
                        principalTable: "CustomerTripInterests",
                        principalColumn: "CustomerTripInterestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CrmStatusChanges",
                columns: table => new
                {
                    CrmStatusChangeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerTripInterestId = table.Column<int>(type: "int", nullable: true),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmStatusChanges", x => x.CrmStatusChangeId);
                    table.ForeignKey(
                        name: "FK_CrmStatusChanges_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "AdminUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmStatusChanges_CustomerTripInterests_CustomerTripInterestId",
                        column: x => x.CustomerTripInterestId,
                        principalTable: "CustomerTripInterests",
                        principalColumn: "CustomerTripInterestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmStatusChanges_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDateSlot_EarlyBird",
                table: "TripDateSlots",
                sql: "(EarlyBirdPrice IS NULL AND EarlyBirdEndsOn IS NULL) OR (EarlyBirdPrice > 0 AND EarlyBirdEndsOn IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDateSlot_MinimumBookedSeats",
                table: "TripDateSlots",
                sql: "MinimumBookedSeats IS NULL OR (MinimumBookedSeats > 0 AND MinimumBookedSeats <= TotalSeats)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDateSlot_PricePerPerson",
                table: "TripDateSlots",
                sql: "PricePerPerson IS NULL OR PricePerPerson > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AlternatePhoneNumber",
                table: "Customers",
                column: "AlternatePhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedAdminUserId",
                table: "Customers",
                column: "AssignedAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CreatedAt",
                table: "Customers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CrmStatus",
                table: "Customers",
                column: "CrmStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LeadSource",
                table: "Customers",
                column: "LeadSource");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCommunications_AdminUserId",
                table: "CrmCommunications",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCommunications_CreatedAt",
                table: "CrmCommunications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCommunications_CrmWhatsAppTemplateId",
                table: "CrmCommunications",
                column: "CrmWhatsAppTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCommunications_CustomerId_CreatedAt",
                table: "CrmCommunications",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmCommunications_CustomerTripInterestId",
                table: "CrmCommunications",
                column: "CustomerTripInterestId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_AssignedAdminUserId",
                table: "CrmFollowUps",
                column: "AssignedAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_CompletedByAdminUserId",
                table: "CrmFollowUps",
                column: "CompletedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_CreatedByAdminUserId",
                table: "CrmFollowUps",
                column: "CreatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_CustomerId",
                table: "CrmFollowUps",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_CustomerTripInterestId",
                table: "CrmFollowUps",
                column: "CustomerTripInterestId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFollowUps_Status_DueDate",
                table: "CrmFollowUps",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmNotes_AdminUserId",
                table: "CrmNotes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmNotes_CustomerId_CreatedAt",
                table: "CrmNotes",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmNotes_CustomerTripInterestId",
                table: "CrmNotes",
                column: "CustomerTripInterestId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmStatusChanges_AdminUserId",
                table: "CrmStatusChanges",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmStatusChanges_CustomerId_CreatedAt",
                table: "CrmStatusChanges",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmStatusChanges_CustomerTripInterestId",
                table: "CrmStatusChanges",
                column: "CustomerTripInterestId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmStatusChanges_ToStatus_CreatedAt",
                table: "CrmStatusChanges",
                columns: new[] { "ToStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmWhatsAppTemplates_IsActive_Category",
                table: "CrmWhatsAppTemplates",
                columns: new[] { "IsActive", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmWhatsAppTemplates_Name",
                table: "CrmWhatsAppTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_BookingId",
                table: "CustomerTripInterests",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_CreatedByAdminUserId",
                table: "CustomerTripInterests",
                column: "CreatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_CustomerId",
                table: "CustomerTripInterests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_InterestedAt",
                table: "CustomerTripInterests",
                column: "InterestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_Status",
                table: "CustomerTripInterests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_TripDateSlotId",
                table: "CustomerTripInterests",
                column: "TripDateSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTripInterests_TripId_TripDateSlotId",
                table: "CustomerTripInterests",
                columns: new[] { "TripId", "TripDateSlotId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AdminUsers_AssignedAdminUserId",
                table: "Customers",
                column: "AssignedAdminUserId",
                principalTable: "AdminUsers",
                principalColumn: "AdminUserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AdminUsers_AssignedAdminUserId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "CrmCommunications");

            migrationBuilder.DropTable(
                name: "CrmFollowUps");

            migrationBuilder.DropTable(
                name: "CrmNotes");

            migrationBuilder.DropTable(
                name: "CrmStatusChanges");

            migrationBuilder.DropTable(
                name: "CrmWhatsAppTemplates");

            migrationBuilder.DropTable(
                name: "CustomerTripInterests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDateSlot_EarlyBird",
                table: "TripDateSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDateSlot_MinimumBookedSeats",
                table: "TripDateSlots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDateSlot_PricePerPerson",
                table: "TripDateSlots");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AlternatePhoneNumber",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AssignedAdminUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CreatedAt",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CrmStatus",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_LeadSource",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EarlyBirdEndsOn",
                table: "TripDateSlots");

            migrationBuilder.DropColumn(
                name: "EarlyBirdPrice",
                table: "TripDateSlots");

            migrationBuilder.DropColumn(
                name: "MinimumBookedSeats",
                table: "TripDateSlots");

            migrationBuilder.DropColumn(
                name: "PricePerPerson",
                table: "TripDateSlots");

            migrationBuilder.DropColumn(
                name: "Vehicle",
                table: "TripDateSlots");

            migrationBuilder.DropColumn(
                name: "AlternatePhoneNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AssignedAdminUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CrmStatus",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CrmSummary",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastContactAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LeadSource",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "WhatsAppAvailable",
                table: "Customers");
        }
    }
}
