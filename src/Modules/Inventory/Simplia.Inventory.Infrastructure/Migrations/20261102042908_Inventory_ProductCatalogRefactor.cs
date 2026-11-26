using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ProductCatalogRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                schema: "inventory",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                schema: "inventory",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "inventory",
                table: "Products",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                schema: "inventory",
                table: "ProductVariants",
                column: "ProductId",
                principalSchema: "inventory",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                schema: "inventory",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                schema: "inventory",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "inventory",
                table: "Products",
                column: "Code",
                filter: "[Code] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                schema: "inventory",
                table: "ProductVariants",
                column: "ProductId",
                principalSchema: "inventory",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
