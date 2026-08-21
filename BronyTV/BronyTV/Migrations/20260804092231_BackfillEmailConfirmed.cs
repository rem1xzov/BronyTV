using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEmailConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Разовый бэкфилл: все существующие на момент применения миграции
            // пользователи получают подтверждение email, чтобы старые аккаунты
            // могли входить без прохождения верификации. Новые пользователи
            // (регистрирующиеся после) получают IsEmailConfirmed = false в коде.
            //
            // Колонки добавляются через ADD COLUMN IF NOT EXISTS — идемпотентно,
            // без конфликта со схемой, которую мог создать DatabaseInitializer.
            migrationBuilder.Sql("""
                ALTER TABLE public."Users"
                    ADD COLUMN IF NOT EXISTS "IsEmailConfirmed" boolean NOT NULL DEFAULT FALSE;

                UPDATE public."Users"
                    SET "IsEmailConfirmed" = TRUE
                    WHERE "IsEmailConfirmed" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public."Users"
                    SET "IsEmailConfirmed" = FALSE;
                """);
        }
    }
}
