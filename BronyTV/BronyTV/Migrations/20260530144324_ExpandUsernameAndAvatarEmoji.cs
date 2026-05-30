using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class ExpandUsernameAndAvatarEmoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "public",
                table: "Users",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarEmoji",
                schema: "public",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarEmoji",
                schema: "public",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "public",
                table: "Users",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25,
                oldNullable: true);
        }
    }
}
