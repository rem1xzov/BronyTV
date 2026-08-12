using BronyTV.DbContext;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations;

[DbContext(typeof(DbBronyTV))]
[Migration("20260812120000_AddEmailConfirmationSecurity")]
public partial class AddEmailConfirmationSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "EmailConfirmationExpiresAtUtc" timestamp with time zone;

            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "EmailConfirmationLastSentAtUtc" timestamp with time zone;

            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "EmailConfirmationFailedAttempts" integer NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "EmailConfirmationExpiresAtUtc";

            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "EmailConfirmationLastSentAtUtc";

            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "EmailConfirmationFailedAttempts";
            """);
    }
}
