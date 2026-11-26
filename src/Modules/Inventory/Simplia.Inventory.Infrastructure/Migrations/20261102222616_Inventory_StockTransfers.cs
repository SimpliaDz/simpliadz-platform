using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_StockTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockTransfers",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.CheckConstraint("CK_StockTransfers_DistinctLocations", "[SourceLocationId] <> [DestinationLocationId]");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Locations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalSchema: "inventory",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Locations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalSchema: "inventory",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.CheckConstraint("CK_StockTransferLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockTransferLines_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalSchema: "inventory",
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ProductVariantId",
                schema: "inventory",
                table: "StockTransferLines",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_StockTransferId_ProductVariantId",
                schema: "inventory",
                table: "StockTransferLines",
                columns: new[] { "StockTransferId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationLocationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceLocationId",
                schema: "inventory",
                table: "StockTransfers",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Status_CreatedAt",
                schema: "inventory",
                table: "StockTransfers",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_TransferNumber",
                schema: "inventory",
                table: "StockTransfers",
                column: "TransferNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransferLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockTransfers",
                schema: "inventory");
        }
    }
}
