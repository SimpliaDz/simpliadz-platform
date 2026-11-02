using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_PriceLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceLists",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceListItems",
                schema: "inventory",
                columns: table => new
                {
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListItems", x => new { x.PriceListId, x.ProductVariantId });
                    table.CheckConstraint("CK_PriceListItems_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_PriceListItems_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "inventory",
                        principalTable: "PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListItems_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_ProductVariantId",
                schema: "inventory",
                table: "PriceListItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_Name_CurrencyCode",
                schema: "inventory",
                table: "PriceLists",
                columns: new[] { "Name", "CurrencyCode" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO [inventory].[PriceLists] ([Id], [Name], [CurrencyCode], [Type], [IsActive])
                VALUES (NEWID(), N'Retail', 'DZD', N'Retail', 1),
                       (NEWID(), N'Wholesale', 'DZD', N'Wholesale', 1);

                INSERT INTO [inventory].[PriceListItems] ([PriceListId], [ProductVariantId], [Price])
                SELECT p.[Id], v.[Id], v.[RetailPrice]
                FROM [inventory].[ProductVariants] v
                CROSS JOIN [inventory].[PriceLists] p
                WHERE p.[Name] = N'Retail' AND p.[CurrencyCode] = 'DZD';

                INSERT INTO [inventory].[PriceListItems] ([PriceListId], [ProductVariantId], [Price])
                SELECT p.[Id], v.[Id], v.[WholesalePrice]
                FROM [inventory].[ProductVariants] v
                CROSS JOIN [inventory].[PriceLists] p
                WHERE p.[Name] = N'Wholesale' AND p.[CurrencyCode] = 'DZD'
                  AND v.[WholesalePrice] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceListItems",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PriceLists",
                schema: "inventory");
        }
    }
}
