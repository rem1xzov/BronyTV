using BronyTV.DbContext;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BronyTV.Migrations;

[DbContext(typeof(DbBronyTV))]
[Migration("20260823140000_AddPasswordResetFields")]
public partial class AddPasswordResetFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "PasswordResetToken" character varying(128);

            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "PasswordResetExpiresAtUtc" timestamp with time zone;

            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "PasswordResetLastSentAtUtc" timestamp with time zone;

            ALTER TABLE public."Users"
                ADD COLUMN IF NOT EXISTS "PasswordResetFailedAttempts" integer NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "PasswordResetToken";

            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "PasswordResetExpiresAtUtc";

            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "PasswordResetLastSentAtUtc";

            ALTER TABLE public."Users"
                DROP COLUMN IF EXISTS "PasswordResetFailedAttempts";
            """);
    }
}
