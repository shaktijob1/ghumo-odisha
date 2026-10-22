using GhumoOdisha.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhumoOdisha.Infrastructure.Migrations
{
    /// <summary>Starter WhatsApp templates for Admin → CRM (once; admins edit or delete them freely).</summary>
    public partial class CrmStarterTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, category, body) in CrmTemplateSeed.Templates)
            {
                migrationBuilder.InsertData(
                    table: "CrmWhatsAppTemplates",
                    columns: new[] { "Name", "Category", "Body", "IsActive", "CreatedAt", "UpdatedAt" },
                    // \r\n → \n in case the source file was checked out with Windows line endings.
                    values: new object[] { name, (int)category, body.Replace("\r\n", "\n").Trim(), true, CrmTemplateSeed.CreatedAt, CrmTemplateSeed.CreatedAt });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _, _) in CrmTemplateSeed.Templates)
            {
                migrationBuilder.DeleteData(table: "CrmWhatsAppTemplates", keyColumn: "Name", keyValue: name);
            }
        }
    }
}
