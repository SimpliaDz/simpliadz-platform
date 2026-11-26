using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CombinationKey",
                schema: "inventory",
                table: "ProductVariants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VariantAttributeValues",
                schema: "inventory",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantAttributeValues", x => new { x.VariantId, x.AttributeValueId });
                    table.ForeignKey(
                        name: "FK_VariantAttributeValues_AttributeValues_AttributeValueId",
                        column: x => x.AttributeValueId,
                        principalSchema: "inventory",
                        principalTable: "AttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantAttributeValues_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "inventory",
                table: "ProductVariants",
                columns: new[] { "ProductId", "CombinationKey" },
                unique: true,
                filter: "[CombinationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributeValues_AttributeValueId",
                schema: "inventory",
                table: "VariantAttributeValues",
                column: "AttributeValueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VariantAttributeValues",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "inventory",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "CombinationKey",
                schema: "inventory",
                table: "ProductVariants");
        }
    }
}
