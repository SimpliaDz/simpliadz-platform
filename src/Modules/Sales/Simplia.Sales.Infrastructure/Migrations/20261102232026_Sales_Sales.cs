using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_Sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sales",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CashierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SaleDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sales_Discount_NonNegative", "[DiscountAmount] >= 0");
                    table.CheckConstraint("CK_Sales_Subtotal_NonNegative", "[Subtotal] >= 0");
                    table.CheckConstraint("CK_Sales_Total_NonNegative", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLines", x => x.Id);
                    table.CheckConstraint("CK_SaleLines_Amounts_NonNegative", "[UnitPrice] >= 0 AND [UnitCostSnapshot] >= 0 AND [DiscountAmount] >= 0 AND [LineTotal] >= 0");
                    table.CheckConstraint("CK_SaleLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_SaleLines_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_ProductVariantId",
                schema: "sales",
                table: "SaleLines",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_SaleId_ProductVariantId",
                schema: "sales",
                table: "SaleLines",
                columns: new[] { "SaleId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId_SaleDate",
                schema: "sales",
                table: "Sales",
                columns: new[] { "CustomerId", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_LocationId_SaleDate",
                schema: "sales",
                table: "Sales",
                columns: new[] { "LocationId", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleNumber",
                schema: "sales",
                table: "Sales",
                column: "SaleNumber",
                unique: true,
                filter: "[SaleNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status",
                schema: "sales",
                table: "Sales",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "Sales",
                schema: "sales");
        }
    }
}
