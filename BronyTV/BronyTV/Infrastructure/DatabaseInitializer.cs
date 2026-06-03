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

    private const string EnsureCommentsTableSql = """
        CREATE TABLE IF NOT EXISTS public."Comments" (
            "Id" uuid NOT NULL,
            "VideoId" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "Text" character varying(500) NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_Comments" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_Comments_Videos_VideoId" FOREIGN KEY ("VideoId")
                REFERENCES public."Videos" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_Comments_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_Comments_VideoId"
            ON public."Comments" ("VideoId");

        CREATE INDEX IF NOT EXISTS "IX_Comments_UserId"
            ON public."Comments" ("UserId");
        """;

    private const string EnsureParentCommentIdColumnSql = """
        ALTER TABLE public."Comments"
            ADD COLUMN IF NOT EXISTS "ParentCommentId" uuid;

        CREATE INDEX IF NOT EXISTS "IX_Comments_ParentCommentId"
            ON public."Comments" ("ParentCommentId");

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_Comments_Comments_ParentCommentId'
            ) THEN
                ALTER TABLE public."Comments"
                    ADD CONSTRAINT "FK_Comments_Comments_ParentCommentId"
                    FOREIGN KEY ("ParentCommentId")
                    REFERENCES public."Comments" ("Id")
                    ON DELETE CASCADE;
            END IF;
        END $$;
        """;

    private const string EnsureCommentLikesTableSql = """
        CREATE TABLE IF NOT EXISTS public."CommentLikes" (
            "UserId" uuid NOT NULL,
            "CommentId" uuid NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_CommentLikes" PRIMARY KEY ("UserId", "CommentId"),
            CONSTRAINT "FK_CommentLikes_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_CommentLikes_Comments_CommentId" FOREIGN KEY ("CommentId")
                REFERENCES public."Comments" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_CommentLikes_CommentId"
            ON public."CommentLikes" ("CommentId");
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
        await EnsureCommentsTableAsync(context, logger, cancellationToken);
        await EnsureParentCommentIdColumnAsync(context, logger, cancellationToken);
        await EnsureCommentLikesTableAsync(context, logger, cancellationToken);
        await EnsureUserCommentBanColumnAsync(context, logger, cancellationToken);
    }

    private const string EnsureUserCommentBanColumnSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "IsBannedFromCommenting" boolean NOT NULL DEFAULT FALSE;
        """;

    public static async Task EnsureUserCommentBanColumnAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUserCommentBanColumnSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\".\"IsBannedFromCommenting\" column.");
    }

    public static async Task EnsureParentCommentIdColumnAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureParentCommentIdColumnSql, cancellationToken);
        logger.LogInformation("Verified public.\"Comments\".\"ParentCommentId\" column and self-reference.");
    }

    public static async Task EnsureCommentLikesTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureCommentLikesTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"CommentLikes\" table exists (CREATE TABLE IF NOT EXISTS).");
    }

    public static async Task EnsureCommentsTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureCommentsTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"Comments\" table exists (CREATE TABLE IF NOT EXISTS).");
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
