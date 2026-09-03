using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyActivityProgress",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ActiveMinutes = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    QualifyingCommentsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsStreakCredited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyActivityProgress", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_DailyActivityProgress_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendingManualRewards",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingManualRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingManualRewards_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreakRewardsClaimed",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Milestone = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RewardDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakRewardsClaimed", x => new { x.UserId, x.Milestone });
                    table.ForeignKey(
                        name: "FK_StreakRewardsClaimed_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserStreaks",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastActiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FreezesAvailable = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    FreezesUsedThisMonth = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FreezesMonth = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PendingFreezeDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStreaks", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserStreaks_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingManualRewards_Status",
                schema: "public",
                table: "PendingManualRewards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PendingManualRewards_UserId",
                schema: "public",
                table: "PendingManualRewards",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyActivityProgress",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PendingManualRewards",
                schema: "public");

            migrationBuilder.DropTable(
                name: "StreakRewardsClaimed",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserStreaks",
                schema: "public");
        }
    }
}
