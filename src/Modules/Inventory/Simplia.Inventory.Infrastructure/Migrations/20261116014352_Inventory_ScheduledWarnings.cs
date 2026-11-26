using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ScheduledWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryWarnings",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false),
                    ReorderPoint = table.Column<int>(type: "int", nullable: true),
                    StockEnteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FirstDetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastDetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryWarnings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryWarnings_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "inventory",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryWarningSettings",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LowStockEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AgeingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    AgeThresholdDays = table.Column<int>(type: "int", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryWarningSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "InventoryWarningSettings",
                columns: new[] { "Id", "AgeThresholdDays", "AgeingEnabled", "Enabled", "IntervalMinutes", "LastRunAt", "LeaseToken", "LeaseUntil", "LowStockEnabled", "NextRunAt" },
                values: new object[] { new Guid("20c7176d-2d2f-4d20-99f0-a99ca2874d40"), 90, true, false, 60, null, null, null, true, new DateTimeOffset(new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWarnings_IsActive_LocationId_Kind",
                schema: "inventory",
                table: "InventoryWarnings",
                columns: new[] { "IsActive", "LocationId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWarnings_LocationId",
                schema: "inventory",
                table: "InventoryWarnings",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWarnings_ProductVariantId_LocationId_Kind",
                schema: "inventory",
                table: "InventoryWarnings",
                columns: new[] { "ProductVariantId", "LocationId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryWarnings",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "InventoryWarningSettings",
                schema: "inventory");
        }
    }
}
