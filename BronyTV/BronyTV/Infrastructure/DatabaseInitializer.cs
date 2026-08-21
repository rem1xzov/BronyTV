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

    private const string EnsureEmailConfirmationColumnsSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "IsEmailConfirmed" boolean NOT NULL DEFAULT FALSE;

        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "EmailConfirmationToken" character varying(128);

        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "EmailConfirmationExpiresAtUtc" timestamp with time zone;

        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "EmailConfirmationLastSentAtUtc" timestamp with time zone;

        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "EmailConfirmationFailedAttempts" integer NOT NULL DEFAULT 0;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_EmailConfirmationToken"
            ON public."Users" ("EmailConfirmationToken")
            WHERE "EmailConfirmationToken" IS NOT NULL;
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
        await EnsureUserPlatformRoleColumnAsync(context, logger, cancellationToken);
        await EnsureEmailConfirmationColumnsAsync(context, logger, cancellationToken);
        await EnsureUserReferralColumnsAsync(context, logger, cancellationToken);
        await EnsureForumTablesAsync(context, logger, cancellationToken);
        await EnsureNewsPostsTableAsync(context, logger, cancellationToken);
        await EnsureSupportTablesAsync(context, logger, cancellationToken);
        await EnsureUserActivityTableAsync(context, logger, cancellationToken);
        await EnsureUserFavoritesTableAsync(context, logger, cancellationToken);
        await EnsureVpnTablesAsync(context, logger, cancellationToken);
    }

    private const string EnsureUserPlatformRoleColumnSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "PlatformRole" character varying(16) NOT NULL DEFAULT 'User';
        """;

    private const string EnsureForumTablesSql = """
        CREATE TABLE IF NOT EXISTS public."ForumThreads" (
            "Id" uuid NOT NULL,
            "Title" character varying(150) NOT NULL,
            "Description" character varying(4000),
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "AuthorId" uuid NOT NULL,
            CONSTRAINT "PK_ForumThreads" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_ForumThreads_Users_AuthorId" FOREIGN KEY ("AuthorId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_ForumThreads_CreatedAtUtc"
            ON public."ForumThreads" ("CreatedAtUtc");

        CREATE TABLE IF NOT EXISTS public."ForumPosts" (
            "Id" uuid NOT NULL,
            "ThreadId" uuid NOT NULL,
            "Content" character varying(4000) NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "AuthorId" uuid NOT NULL,
            CONSTRAINT "PK_ForumPosts" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_ForumPosts_ForumThreads_ThreadId" FOREIGN KEY ("ThreadId")
                REFERENCES public."ForumThreads" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ForumPosts_Users_AuthorId" FOREIGN KEY ("AuthorId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_ForumPosts_ThreadId"
            ON public."ForumPosts" ("ThreadId");

        CREATE INDEX IF NOT EXISTS "IX_ForumPosts_CreatedAtUtc"
            ON public."ForumPosts" ("CreatedAtUtc");

        ALTER TABLE public."ForumThreads"
            ADD COLUMN IF NOT EXISTS "Images" text;

        ALTER TABLE public."ForumPosts"
            ADD COLUMN IF NOT EXISTS "Images" text;

                ALTER TABLE public."ForumPosts"
            ADD COLUMN IF NOT EXISTS "LikedUserIds" text;

        ALTER TABLE public."ForumPosts"
            ADD COLUMN IF NOT EXISTS "ReplyToPostId" uuid;

        CREATE INDEX IF NOT EXISTS "IX_ForumPosts_ReplyToPostId"
            ON public."ForumPosts" ("ReplyToPostId");

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_ForumPosts_ForumPosts_ReplyToPostId'
            ) THEN
                ALTER TABLE public."ForumPosts"
                    ADD CONSTRAINT "FK_ForumPosts_ForumPosts_ReplyToPostId"
                    FOREIGN KEY ("ReplyToPostId")
                    REFERENCES public."ForumPosts" ("Id")
                    ON DELETE CASCADE;
            END IF;
        END $$;
        """;

    private const string EnsureNewsPostsTableSql = """
        CREATE TABLE IF NOT EXISTS public."NewsPosts" (
            "Id" uuid NOT NULL,
            "Title" character varying(200),
            "Content" character varying(10000),
            "ImageUrl" character varying(500),
            "AuthorUsername" character varying(100) NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_NewsPosts" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_NewsPosts_CreatedAt"
            ON public."NewsPosts" ("CreatedAt");
        """;

        private const string EnsureUserActivityTableSql = """
        CREATE TABLE IF NOT EXISTS public."UserActivities" (
            "Id" bigint GENERATED BY DEFAULT AS IDENTITY NOT NULL,
            "UserId" uuid NOT NULL,
            "ActivityType" character varying(32) NOT NULL,
            "Details" character varying(200),
            "Timestamp" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_UserActivities" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_UserActivities_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

                CREATE INDEX IF NOT EXISTS "IX_UserActivities_UserId_Timestamp"
            ON public."UserActivities" ("UserId", "Timestamp");
        """;

        private const string EnsureUserFavoritesTableSql = """
        CREATE TABLE IF NOT EXISTS public."UserFavorites" (
            "Id" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "VideoId" uuid NOT NULL,
            "AddedAt" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_UserFavorites" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_UserFavorites_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_UserFavorites_Videos_VideoId" FOREIGN KEY ("VideoId")
                REFERENCES public."Videos" ("Id") ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserFavorites_UserId_VideoId"
            ON public."UserFavorites" ("UserId", "VideoId");
        """;

    private const string EnsureUserReferralColumnsSql = """
        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "ReferralCode" character varying(16);

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_ReferralCode"
            ON public."Users" ("ReferralCode")
            WHERE "ReferralCode" IS NOT NULL;

        ALTER TABLE public."Users"
            ADD COLUMN IF NOT EXISTS "ReferredByUserId" uuid;

        CREATE INDEX IF NOT EXISTS "IX_Users_ReferredByUserId"
            ON public."Users" ("ReferredByUserId");

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'FK_Users_Users_ReferredByUserId'
            ) THEN
                ALTER TABLE public."Users"
                    ADD CONSTRAINT "FK_Users_Users_ReferredByUserId"
                    FOREIGN KEY ("ReferredByUserId")
                    REFERENCES public."Users" ("Id")
                    ON DELETE SET NULL;
            END IF;
        END $$;
        """;

    private const string EnsureVpnTablesSql = """
        CREATE TABLE IF NOT EXISTS public."VpnSubscriptions" (
            "Id" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "Kind" character varying(16) NOT NULL,
            "PlanName" character varying(100) NOT NULL,
            "StartedAtUtc" timestamp with time zone NOT NULL,
            "ExpiresAtUtc" timestamp with time zone,
            "ClientUuid" character varying(64),
            "Note" character varying(500),
            "IsRevoked" boolean NOT NULL DEFAULT FALSE,
            "PanelPlanNameId" character varying(32),
            CONSTRAINT "PK_VpnSubscriptions" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_VpnSubscriptions_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_VpnSubscriptions_UserId"
            ON public."VpnSubscriptions" ("UserId");

        CREATE TABLE IF NOT EXISTS public."VpnPromoKeys" (
            "Code" character varying(16) NOT NULL,
            "IsUsed" boolean NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UsedAtUtc" timestamp with time zone,
            "UsedByUserId" uuid,
            "SubscriptionId" uuid,
            CONSTRAINT "PK_VpnPromoKeys" PRIMARY KEY ("Code"),
            CONSTRAINT "FK_VpnPromoKeys_Users_UsedByUserId" FOREIGN KEY ("UsedByUserId")
                REFERENCES public."Users" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_VpnPromoKeys_VpnSubscriptions_SubscriptionId" FOREIGN KEY ("SubscriptionId")
                REFERENCES public."VpnSubscriptions" ("Id") ON DELETE SET NULL
        );

                CREATE INDEX IF NOT EXISTS "IX_VpnPromoKeys_IsUsed"
            ON public."VpnPromoKeys" ("IsUsed");

                ALTER TABLE public."VpnPromoKeys"
            ADD COLUMN IF NOT EXISTS "ClientUuid" character varying(64);

        ALTER TABLE public."VpnPromoKeys"
            ADD COLUMN IF NOT EXISTS "DurationMonths" integer NOT NULL DEFAULT 1;

        CREATE TABLE IF NOT EXISTS public."ReferralRewards" (
            "Id" uuid NOT NULL,
            "ReferrerId" uuid NOT NULL,
            "ReferralUserId" uuid NOT NULL,
            "BonusDays" integer NOT NULL,
            "Reason" character varying(32) NOT NULL,
            "IsRedeemed" boolean NOT NULL DEFAULT FALSE,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_ReferralRewards" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_ReferralRewards_Users_ReferrerId" FOREIGN KEY ("ReferrerId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ReferralRewards_Users_ReferralUserId" FOREIGN KEY ("ReferralUserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_ReferralRewards_ReferrerId"
            ON public."ReferralRewards" ("ReferrerId");

        CREATE INDEX IF NOT EXISTS "IX_ReferralRewards_ReferralUserId"
            ON public."ReferralRewards" ("ReferralUserId");
        """;

    private const string EnsureSupportTablesSql = """
        CREATE TABLE IF NOT EXISTS public."SupportTickets" (
            "Id" uuid NOT NULL,
            "UserId" uuid NOT NULL,
            "Title" character varying(150) NOT NULL,
            "IsClosed" boolean NOT NULL DEFAULT FALSE,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SupportTickets_Users_UserId" FOREIGN KEY ("UserId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_SupportTickets_UserId"
            ON public."SupportTickets" ("UserId");

        CREATE INDEX IF NOT EXISTS "IX_SupportTickets_CreatedAtUtc"
            ON public."SupportTickets" ("CreatedAtUtc");

        CREATE INDEX IF NOT EXISTS "IX_SupportTickets_IsClosed"
            ON public."SupportTickets" ("IsClosed");

        CREATE TABLE IF NOT EXISTS public."SupportMessages" (
            "Id" uuid NOT NULL,
            "TicketId" uuid NOT NULL,
            "SenderId" uuid NOT NULL,
            "Content" character varying(4000) NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_SupportMessages" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SupportMessages_SupportTickets_TicketId" FOREIGN KEY ("TicketId")
                REFERENCES public."SupportTickets" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SupportMessages_Users_SenderId" FOREIGN KEY ("SenderId")
                REFERENCES public."Users" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_SupportMessages_TicketId"
            ON public."SupportMessages" ("TicketId");

        CREATE INDEX IF NOT EXISTS "IX_SupportMessages_CreatedAtUtc"
            ON public."SupportMessages" ("CreatedAtUtc");
        """;

    public static async Task EnsureUserPlatformRoleColumnAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUserPlatformRoleColumnSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\".\"PlatformRole\" column.");
    }

    public static async Task EnsureEmailConfirmationColumnsAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureEmailConfirmationColumnsSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\" email confirmation columns and unique index.");
    }

    public static async Task EnsureForumTablesAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureForumTablesSql, cancellationToken);
        logger.LogInformation("Verified public forum tables exist (CREATE TABLE IF NOT EXISTS).");
    }

    public static async Task EnsureNewsPostsTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureNewsPostsTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"NewsPosts\" table exists.");
    }

        public static async Task EnsureSupportTablesAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureSupportTablesSql, cancellationToken);
        logger.LogInformation("Verified public support tables exist (CREATE TABLE IF NOT EXISTS).");
    }

        public static async Task EnsureUserActivityTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUserActivityTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"UserActivities\" table exists (CREATE TABLE IF NOT EXISTS).");
    }

        public static async Task EnsureUserFavoritesTableAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUserFavoritesTableSql, cancellationToken);
        logger.LogInformation("Verified public.\"UserFavorites\" table exists (CREATE TABLE IF NOT EXISTS).");
    }

    public static async Task EnsureUserReferralColumnsAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureUserReferralColumnsSql, cancellationToken);
        logger.LogInformation("Verified public.\"Users\" referral columns and unique index.");
    }

    public static async Task EnsureVpnTablesAsync(
        DbBronyTV context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(EnsureVpnTablesSql, cancellationToken);
        logger.LogInformation("Verified public VPN tables exist (CREATE TABLE IF NOT EXISTS).");
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
