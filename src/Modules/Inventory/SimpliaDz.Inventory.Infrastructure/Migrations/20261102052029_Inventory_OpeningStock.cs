using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_OpeningStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_NonZero",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OpeningBalance_Once",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "VariantId", "LocationId" },
                unique: true,
                filter: "[Type] = 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_Valid",
                schema: "inventory",
                table: "StockMovements",
                sql: "([Type] = 7 AND [QuantityDelta] >= 0) OR ([Type] <> 7 AND [QuantityDelta] <> 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OpeningBalance_Once",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_Valid",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_NonZero",
                schema: "inventory",
                table: "StockMovements",
                sql: "[QuantityDelta] <> 0");
        }
    }
}
