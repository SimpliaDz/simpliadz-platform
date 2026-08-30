using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_BusinessLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_Name",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "inventory",
                table: "Locations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inventory",
                table: "Locations",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "inventory",
                table: "Locations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "inventory",
                table: "Locations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name",
                schema: "inventory",
                table: "Locations",
                column: "Name");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Locations_Type",
                schema: "inventory",
                table: "Locations",
                sql: "[Type] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_Name",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Locations_Type",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "inventory",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name",
                schema: "inventory",
                table: "Locations",
                column: "Name",
                unique: true);
        }
    }
}
