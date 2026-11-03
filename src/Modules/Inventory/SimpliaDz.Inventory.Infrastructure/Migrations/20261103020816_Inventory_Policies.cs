using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_Policies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryPolicies",
                schema: "inventory",
                columns: table => new
                {
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReorderPoint = table.Column<int>(type: "int", nullable: false),
                    TargetStockLevel = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPolicies", x => new { x.ProductVariantId, x.LocationId });
                    table.CheckConstraint("CK_InventoryPolicies_ReorderPoint_NonNegative", "[ReorderPoint] >= 0");
                    table.CheckConstraint("CK_InventoryPolicies_TargetStockLevel_Valid", "[TargetStockLevel] IS NULL OR [TargetStockLevel] >= [ReorderPoint]");
                    table.ForeignKey(
                        name: "FK_InventoryPolicies_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryPolicies_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPolicies_LocationId",
                schema: "inventory",
                table: "InventoryPolicies",
                column: "LocationId");

            migrationBuilder.Sql("""
                INSERT INTO [inventory].[InventoryPolicies]
                    ([ProductVariantId], [LocationId], [ReorderPoint], [TargetStockLevel], [UpdatedAt])
                SELECT [VariantId], [LocationId], [ReorderPoint], NULL, SYSUTCDATETIME()
                FROM [inventory].[StockItems]
                WHERE [ReorderPoint] IS NOT NULL;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockItems_ReorderPoint_NonNegative",
                schema: "inventory",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                schema: "inventory",
                table: "StockItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReorderPoint",
                schema: "inventory",
                table: "StockItems",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE stock
                SET stock.[ReorderPoint] = policy.[ReorderPoint]
                FROM [inventory].[StockItems] AS stock
                INNER JOIN [inventory].[InventoryPolicies] AS policy
                    ON policy.[ProductVariantId] = stock.[VariantId]
                   AND policy.[LocationId] = stock.[LocationId];
                """);

            migrationBuilder.DropTable(
                name: "InventoryPolicies",
                schema: "inventory");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockItems_ReorderPoint_NonNegative",
                schema: "inventory",
                table: "StockItems",
                sql: "[ReorderPoint] IS NULL OR [ReorderPoint] >= 0");
        }
    }
}
