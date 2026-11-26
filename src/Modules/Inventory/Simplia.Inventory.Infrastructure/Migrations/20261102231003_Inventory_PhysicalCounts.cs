using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_PhysicalCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryCounts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCountLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: false),
                    CountedQuantity = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCountLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryCountLines_CountedQuantity_NonNegative", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
                    table.CheckConstraint("CK_InventoryCountLines_ExpectedQuantity_NonNegative", "[ExpectedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_InventoryCounts_InventoryCountId",
                        column: x => x.InventoryCountId,
                        principalSchema: "inventory",
                        principalTable: "InventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_InventoryCountId_ProductVariantId",
                schema: "inventory",
                table: "InventoryCountLines",
                columns: new[] { "InventoryCountId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_ProductVariantId",
                schema: "inventory",
                table: "InventoryCountLines",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_LocationId_StartedAt",
                schema: "inventory",
                table: "InventoryCounts",
                columns: new[] { "LocationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_Status",
                schema: "inventory",
                table: "InventoryCounts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryCountLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "InventoryCounts",
                schema: "inventory");
        }
    }
}
