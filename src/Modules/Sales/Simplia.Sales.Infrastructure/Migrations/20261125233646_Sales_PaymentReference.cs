using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_PaymentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "sales",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "sales",
                table: "Payments");
        }
    }
}
