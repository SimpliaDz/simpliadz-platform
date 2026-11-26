using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_DocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesDocumentNumbers",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IssueYear = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DisplayNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDocumentNumbers", x => x.Id);
                    table.CheckConstraint("CK_SalesDocumentNumbers_Sequence", "[SequenceNumber] > 0");
                    table.CheckConstraint("CK_SalesDocumentNumbers_Year", "[IssueYear] >= 2000 AND [IssueYear] <= 9999");
                    table.ForeignKey(
                        name: "FK_SalesDocumentNumbers_Sales_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "sales",
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentNumbers_DocumentType_IssueYear_DisplayNumber",
                schema: "sales",
                table: "SalesDocumentNumbers",
                columns: new[] { "DocumentType", "IssueYear", "DisplayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentNumbers_DocumentType_IssueYear_SequenceNumber",
                schema: "sales",
                table: "SalesDocumentNumbers",
                columns: new[] { "DocumentType", "IssueYear", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentNumbers_SaleId_DocumentType",
                schema: "sales",
                table: "SalesDocumentNumbers",
                columns: new[] { "SaleId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesDocumentNumbers",
                schema: "sales");
        }
    }
}
