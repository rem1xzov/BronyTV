using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class AddUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "public",
                table: "Users",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "public",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "\"Username\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "public",
                table: "Users");
        }
    }
}
