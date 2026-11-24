using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Organization_Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationSettings",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValueType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSettings_Key",
                schema: "organization",
                table: "OrganizationSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationSettings",
                schema: "organization");
        }
    }
}
