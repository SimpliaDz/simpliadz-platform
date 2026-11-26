using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Purchasing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Purchasing_PurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "purchasing",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                schema: "purchasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderLines_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
                    table.CheckConstraint("CK_PurchaseOrderLines_ReceivedQuantity_Range", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]");
                    table.CheckConstraint("CK_PurchaseOrderLines_UnitCost_NonNegative", "[UnitCost] >= 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "purchasing",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ProductVariantId",
                schema: "purchasing",
                table: "PurchaseOrderLines",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_ProductVariantId",
                schema: "purchasing",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DestinationLocationId",
                schema: "purchasing",
                table: "PurchaseOrders",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PurchaseOrderNumber",
                schema: "purchasing",
                table: "PurchaseOrders",
                column: "PurchaseOrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status_OrderDate",
                schema: "purchasing",
                table: "PurchaseOrders",
                columns: new[] { "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId_OrderDate",
                schema: "purchasing",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "OrderDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderLines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "purchasing");
        }
    }
}
