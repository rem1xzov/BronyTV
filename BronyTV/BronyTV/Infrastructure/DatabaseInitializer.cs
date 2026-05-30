using BronyTV.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BronyTV.Infrastructure;

public static class DatabaseInitializer
{
    private const string EnsureUsersTableSql = """
        CREATE TABLE IF NOT EXISTS public."Users" (
            "Id" uuid NOT NULL,
            "Email" character varying(320) NOT NULL,
            "PasswordHash" character varying(200) NOT NULL,
            "Race" character varying(32) NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "RaceSelectedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email"
            ON public."Users" ("Email");
        """;

    private const string EnsureUsernameColumnSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "Username" character varying(25);

        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'Users'
                  AND column_name = 'Username'
                  AND character_maximum_length < 25
            ) THEN
                ALTER TABLE public."Users"
                    ALTER COLUMN "Username" TYPE character varying(25);
            END IF;
        END $$;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username"
            ON public."Users" ("Username")
            WHERE "Username" IS NOT NULL;
        """;

    private const string EnsureAvatarEmojiColumnSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "AvatarEmoji" character varying(32);
        """;

    public static async Task ApplyMigrationsAndEnsureSchemaAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying pending EF migrations: {Migrations}", string.Join(", ", pending));
        }
        else
        {
            logger.LogInformation("No pending EF migrations detected.");
        }

        await context.Database.MigrateAsync(cancellationToken);
        await EnsureUsersTableAsync(context, logger, cancellationToken);
        await EnsureUsernameColumnAsync(context, logger, cancellationToken);
        await EnsureAvatarEmojiColumnAsync(context, logger, cancellationToken);
    }

    public static async Task EnsureAvatarEmojiColumnAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureAvatarEmojiColumnSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\".\"AvatarEmoji\" column.");
    }

    public static async Task EnsureUsernameColumnAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUsernameColumnSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\".\"Username\" column and unique index.");
    }

    public static async Task EnsureUsersTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUsersTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\" table exists (CREATE TABLE IF NOT EXISTS).");
    }
}
