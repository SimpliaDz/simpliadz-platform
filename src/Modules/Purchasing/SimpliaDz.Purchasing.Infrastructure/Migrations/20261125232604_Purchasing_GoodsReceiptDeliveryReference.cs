using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Purchasing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Purchasing_GoodsReceiptDeliveryReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryReference",
                schema: "purchasing",
                table: "GoodsReceipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryReference",
                schema: "purchasing",
                table: "GoodsReceipts");
        }
    }
}
