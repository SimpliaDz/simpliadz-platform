using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeValues",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeValues_ProductAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalSchema: "inventory",
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_AttributeId_DisplayOrder",
                schema: "inventory",
                table: "AttributeValues",
                columns: new[] { "AttributeId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_AttributeId_Value",
                schema: "inventory",
                table: "AttributeValues",
                columns: new[] { "AttributeId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_Name",
                schema: "inventory",
                table: "ProductAttributes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttributeValues",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ProductAttributes",
                schema: "inventory");
        }
    }
}
