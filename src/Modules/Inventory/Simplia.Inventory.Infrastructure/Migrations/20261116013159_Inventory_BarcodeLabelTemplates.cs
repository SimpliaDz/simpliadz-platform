using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_BarcodeLabelTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BarcodeLabelTemplates",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PageWidthMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    PageHeightMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    LabelWidthMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    LabelHeightMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MarginMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    GapMm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ShowName = table.Column<bool>(type: "bit", nullable: false),
                    ShowSku = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarcodeLabelTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "BarcodeLabelTemplates",
                columns: new[] { "Id", "CreatedAt", "GapMm", "IsActive", "LabelHeightMm", "LabelWidthMm", "MarginMm", "Name", "PageHeightMm", "PageWidthMm", "ShowName", "ShowSku", "UpdatedAt" },
                values: new object[] { new Guid("5369ed28-f57a-49be-a587-13f4d0d091f1"), new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3m, true, 40m, 90m, 5m, "Standard A4 90 x 40 mm", 297m, 210m, true, true, new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_BarcodeLabelTemplates_Name",
                schema: "inventory",
                table: "BarcodeLabelTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BarcodeLabelTemplates",
                schema: "inventory");
        }
    }
}
