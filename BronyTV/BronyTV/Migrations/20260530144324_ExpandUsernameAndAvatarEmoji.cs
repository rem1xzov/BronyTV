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
            // See AddUsername: on a fresh database Users is created by the following
            // InitialEmailAuth migration. The post-migration schema initializer then
            // applies these columns idempotently.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."Users"') IS NOT NULL THEN
                        ALTER TABLE public."Users"
                            ADD COLUMN IF NOT EXISTS "Username" character varying(25);

                        ALTER TABLE public."Users"
                            ALTER COLUMN "Username" TYPE character varying(25);

                        ALTER TABLE public."Users"
                            ADD COLUMN IF NOT EXISTS "AvatarEmoji" character varying(32);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."Users"') IS NOT NULL THEN
                        ALTER TABLE public."Users"
                            DROP COLUMN IF EXISTS "AvatarEmoji";

                        ALTER TABLE public."Users"
                            ALTER COLUMN "Username" TYPE character varying(15);
                    END IF;
                END $$;
                """);
        }
    }
}
