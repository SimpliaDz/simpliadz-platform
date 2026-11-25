using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_Promotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                schema: "sales",
                table: "SaleLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionName",
                schema: "sales",
                table: "SaleLines",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionUnitDiscount",
                schema: "sales",
                table: "SaleLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive_Target_TargetId",
                schema: "sales",
                table: "Promotions",
                columns: new[] { "IsActive", "Target", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "sales");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                schema: "sales",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "PromotionName",
                schema: "sales",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "PromotionUnitDiscount",
                schema: "sales",
                table: "SaleLines");
        }
    }
}
