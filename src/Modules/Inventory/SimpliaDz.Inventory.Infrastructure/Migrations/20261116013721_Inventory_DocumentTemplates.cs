using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_DocumentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryDocumentTemplates",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Columns = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PaperSize = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Landscape = table.Column<bool>(type: "bit", nullable: false),
                    FooterText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "InventoryDocumentTemplates",
                columns: new[] { "Id", "Columns", "CreatedAt", "FooterText", "IsActive", "Landscape", "Name", "PaperSize", "Title", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11a37a5b-14c9-444b-a858-71dc0cb2fd47"), "Product,Sku,Barcode,Quantity", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Standard stock list", "A4", "Inventory stock list", "StockList", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("22a37a5b-14c9-444b-a858-71dc0cb2fd47"), "Product,Sku,Barcode,Counted", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Standard count sheet", "A4", "Physical inventory count", "CountSheet", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("33a37a5b-14c9-444b-a858-71dc0cb2fd47"), "Product,Sku,Expected,Counted,Difference", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Standard count differences", "A4", "Inventory count differences", "CountDifferences", new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentTemplates_Name",
                schema: "inventory",
                table: "InventoryDocumentTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryDocumentTemplates",
                schema: "inventory");
        }
    }
}
