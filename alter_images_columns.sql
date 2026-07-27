-- Изменяем тип колонки Images на text, чтобы хранить JSON-строку
ALTER TABLE "ForumThreads" ALTER COLUMN "Images" TYPE text;
ALTER TABLE "ForumPosts"   ALTER COLUMN "Images" TYPE text;
