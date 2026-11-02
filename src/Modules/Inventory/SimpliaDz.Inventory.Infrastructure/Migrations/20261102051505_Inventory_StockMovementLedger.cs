using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_StockMovementLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "inventory",
                table: "StockMovements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_NonZero",
                schema: "inventory",
                table: "StockMovements",
                sql: "[QuantityDelta] <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_QuantityDelta_NonZero",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "inventory",
                table: "StockMovements");
        }
    }
}
