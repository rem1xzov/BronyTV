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
            // This historical migration predates InitialEmailAuth in the original
            // repository, so Users does not exist on a completely fresh database.
            // Keep the migration ID for deployed databases, but make it safe on both
            // fresh and pre-existing schemas. DatabaseInitializer adds the column after
            // all migrations when the table is created by InitialEmailAuth later.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."Users"') IS NOT NULL THEN
                        ALTER TABLE public."Users"
                            ADD COLUMN IF NOT EXISTS "Username" character varying(15);

                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username"
                            ON public."Users" ("Username")
                            WHERE "Username" IS NOT NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS public."IX_Users_Username";

                DO $$
                BEGIN
                    IF to_regclass('public."Users"') IS NOT NULL THEN
                        ALTER TABLE public."Users"
                            DROP COLUMN IF EXISTS "Username";
                    END IF;
                END $$;
                """);
        }
    }
}
