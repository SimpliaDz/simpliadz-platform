using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Simplia.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Identity_UserLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                schema: "identity",
                table: "AspNetUsers",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanguageCode",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
