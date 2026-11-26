using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ProductAttributeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "inventory",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "CombinationKey",
                schema: "inventory",
                table: "ProductVariants");

            migrationBuilder.CreateTable(
                name: "ProductAttributeAssignments",
                schema: "inventory",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeAssignments", x => new { x.ProductId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ProductAttributeAssignments_ProductAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalSchema: "inventory",
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductAttributeAssignments_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeAssignments_AttributeId",
                schema: "inventory",
                table: "ProductAttributeAssignments",
                column: "AttributeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAttributeAssignments",
                schema: "inventory");

            migrationBuilder.AddColumn<string>(
                name: "CombinationKey",
                schema: "inventory",
                table: "ProductVariants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "inventory",
                table: "ProductVariants",
                columns: new[] { "ProductId", "CombinationKey" },
                unique: true,
                filter: "[CombinationKey] IS NOT NULL");
        }
    }
}
