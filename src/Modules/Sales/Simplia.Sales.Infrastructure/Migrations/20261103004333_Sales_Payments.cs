using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_Payments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                schema: "sales",
                table: "Sales",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_PaidAmount_Range",
                schema: "sales",
                table: "Sales",
                sql: "[PaidAmount] >= 0 AND [PaidAmount] <= [Total]");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SaleId_PaidAt",
                schema: "sales",
                table: "Payments",
                columns: new[] { "SaleId", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_PaidAmount_Range",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                schema: "sales",
                table: "Sales");
        }
    }
}
