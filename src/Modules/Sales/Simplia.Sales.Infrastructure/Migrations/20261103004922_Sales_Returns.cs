using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_Returns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                schema: "sales",
                table: "SaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OriginalSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_OriginalSaleId",
                        column: x => x.OriginalSaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalSaleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitRefundAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Restock = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnLines", x => x.Id);
                    table.CheckConstraint("CK_SaleReturnLines_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_SaleReturnLines_Refund_NonNegative", "[UnitRefundAmount] >= 0 AND [RefundAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleLines_OriginalSaleLineId",
                        column: x => x.OriginalSaleLineId,
                        principalSchema: "sales",
                        principalTable: "SaleLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalSchema: "sales",
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleLines_ReturnedQuantity_Range",
                schema: "sales",
                table: "SaleLines",
                sql: "[ReturnedQuantity] >= 0 AND [ReturnedQuantity] <= [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_OriginalSaleLineId",
                schema: "sales",
                table: "SaleReturnLines",
                column: "OriginalSaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_ProductVariantId",
                schema: "sales",
                table: "SaleReturnLines",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleReturnId_OriginalSaleLineId",
                schema: "sales",
                table: "SaleReturnLines",
                columns: new[] { "SaleReturnId", "OriginalSaleLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_LocationId",
                schema: "sales",
                table: "SaleReturns",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_OriginalSaleId_ReturnDate",
                schema: "sales",
                table: "SaleReturns",
                columns: new[] { "OriginalSaleId", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_ReturnNumber",
                schema: "sales",
                table: "SaleReturns",
                column: "ReturnNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleReturnLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SaleReturns",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleLines_ReturnedQuantity_Range",
                schema: "sales",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                schema: "sales",
                table: "SaleLines");
        }
    }
}
