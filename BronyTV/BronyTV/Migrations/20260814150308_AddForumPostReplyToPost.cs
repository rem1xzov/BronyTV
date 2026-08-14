using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class AddForumPostReplyToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToPostId",
                schema: "public",
                table: "ForumPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_ReplyToPostId",
                schema: "public",
                table: "ForumPosts",
                column: "ReplyToPostId");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumPosts_ForumPosts_ReplyToPostId",
                schema: "public",
                table: "ForumPosts",
                column: "ReplyToPostId",
                principalSchema: "public",
                principalTable: "ForumPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumPosts_ForumPosts_ReplyToPostId",
                schema: "public",
                table: "ForumPosts");

            migrationBuilder.DropIndex(
                name: "IX_ForumPosts_ReplyToPostId",
                schema: "public",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "ReplyToPostId",
                schema: "public",
                table: "ForumPosts");
        }
    }
}
