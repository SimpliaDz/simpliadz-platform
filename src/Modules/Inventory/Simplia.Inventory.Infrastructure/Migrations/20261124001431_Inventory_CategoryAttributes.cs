using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_CategoryAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryAttributes",
                schema: "inventory",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryAttributes", x => new { x.CategoryId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_CategoryAttributes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "inventory",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryAttributes_ProductAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalSchema: "inventory",
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAttributes_AttributeId",
                schema: "inventory",
                table: "CategoryAttributes",
                column: "AttributeId");

            migrationBuilder.Sql("""
                INSERT INTO [inventory].[CategoryAttributes] ([CategoryId], [AttributeId])
                SELECT DISTINCT product.[ProductCategoryId], assignment.[AttributeId]
                FROM [inventory].[ProductAttributeAssignments] AS assignment
                INNER JOIN [inventory].[Products] AS product ON product.[Id] = assignment.[ProductId]
                WHERE product.[ProductCategoryId] IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "ProductAttributeAssignments",
                schema: "inventory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryAttributes",
                schema: "inventory");

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
    }
}
