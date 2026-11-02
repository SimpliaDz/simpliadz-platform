using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpliaDz.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_ProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCategoryId",
                schema: "inventory",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "inventory",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                schema: "inventory",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                schema: "inventory",
                table: "Categories",
                column: "ParentCategoryId",
                principalSchema: "inventory",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ParentCategoryId",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                schema: "inventory",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
