using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Organization_WorkedHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "organization",
                table: "Organizations",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "Africa/Algiers");

            migrationBuilder.CreateTable(
                name: "WorkSessions",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClockInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClockOutAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClockedOutBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessions", x => x.Id);
                    table.CheckConstraint("CK_WorkSessions_ClockOrder", "[ClockOutAt] IS NULL OR [ClockOutAt] > [ClockInAt]");
                });

            migrationBuilder.CreateTable(
                name: "WorkSessionCorrections",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldClockInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OldClockOutAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NewClockInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NewClockOutAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrectedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessionCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSessionCorrections_WorkSessions_WorkSessionId",
                        column: x => x.WorkSessionId,
                        principalSchema: "organization",
                        principalTable: "WorkSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessionCorrections_WorkSessionId_CorrectedAt",
                schema: "organization",
                table: "WorkSessionCorrections",
                columns: new[] { "WorkSessionId", "CorrectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId",
                schema: "organization",
                table: "WorkSessions",
                column: "UserId",
                unique: true,
                filter: "[ClockOutAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_UserId_ClockInAt",
                schema: "organization",
                table: "WorkSessions",
                columns: new[] { "UserId", "ClockInAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkSessionCorrections",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "WorkSessions",
                schema: "organization");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "organization",
                table: "Organizations");
        }
    }
}
