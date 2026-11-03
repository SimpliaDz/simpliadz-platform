using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_PosProductNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_PosLookup",
                schema: "inventory",
                table: "Products",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name_PosLookup",
                schema: "inventory",
                table: "Products");
        }
    }
}
