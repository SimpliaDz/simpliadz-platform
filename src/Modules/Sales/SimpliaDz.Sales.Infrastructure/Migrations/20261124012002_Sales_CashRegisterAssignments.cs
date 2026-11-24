using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sales_CashRegisterAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashRegisterAssignments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashRegisterAssignments_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "sales",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashRegisterAssignments_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "sales",
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisterAssignments_CashRegisterId",
                schema: "sales",
                table: "CashRegisterAssignments",
                column: "CashRegisterId",
                unique: true,
                filter: "[ReleasedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisterAssignments_CashSessionId",
                schema: "sales",
                table: "CashRegisterAssignments",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisterAssignments_UserId",
                schema: "sales",
                table: "CashRegisterAssignments",
                column: "UserId",
                unique: true,
                filter: "[ReleasedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashRegisterAssignments",
                schema: "sales");
        }
    }
}
