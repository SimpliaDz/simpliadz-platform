using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_CashSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashSessionId",
                schema: "sales",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashRegisters",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CountedCash = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.CheckConstraint("CK_CashSessions_ClosingCash", "([Status] = 'Open' AND [ClosedAt] IS NULL AND [CountedCash] IS NULL AND [ExpectedCash] IS NULL) OR ([Status] = 'Closed' AND [ClosedAt] IS NOT NULL AND [CountedCash] >= 0 AND [ExpectedCash] >= 0)");
                    table.CheckConstraint("CK_CashSessions_OpeningCash", "[OpeningCash] >= 0");
                    table.ForeignKey(
                        name: "FK_CashSessions_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "sales",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountDelta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.CheckConstraint("CK_CashMovements_NonZero", "[AmountDelta] <> 0");
                    table.ForeignKey(
                        name: "FK_CashMovements_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "sales",
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashSessionId_PaymentMethod",
                schema: "sales",
                table: "Payments",
                columns: new[] { "CashSessionId", "PaymentMethod" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashSessionId_OccurredAt",
                schema: "sales",
                table: "CashMovements",
                columns: new[] { "CashSessionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_LocationId",
                schema: "sales",
                table: "CashRegisters",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_LocationId_Code",
                schema: "sales",
                table: "CashRegisters",
                columns: new[] { "LocationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_CashRegisterId",
                schema: "sales",
                table: "CashSessions",
                column: "CashRegisterId",
                unique: true,
                filter: "[Status] = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OpenedAt",
                schema: "sales",
                table: "CashSessions",
                column: "OpenedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CashSessions_CashSessionId",
                schema: "sales",
                table: "Payments",
                column: "CashSessionId",
                principalSchema: "sales",
                principalTable: "CashSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CashSessions_CashSessionId",
                schema: "sales",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "CashMovements",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "CashSessions",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "CashRegisters",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CashSessionId_PaymentMethod",
                schema: "sales",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CashSessionId",
                schema: "sales",
                table: "Payments");
        }
    }
}
