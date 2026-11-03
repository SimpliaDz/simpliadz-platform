using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_Discounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                schema: "sales",
                table: "Sales",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "sales",
                table: "Sales",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                schema: "sales",
                table: "SaleLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "sales",
                table: "SaleLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountType",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                schema: "sales",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "sales",
                table: "SaleLines");
        }
    }
}
