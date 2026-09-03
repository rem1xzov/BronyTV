using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakRewardSeen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRewardSeen",
                schema: "public",
                table: "StreakRewardsClaimed",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRewardSeen",
                schema: "public",
                table: "StreakRewardsClaimed");
        }
    }
}
