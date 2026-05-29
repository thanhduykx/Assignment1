using DataAccessLayer.Context;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Schema;

internal static class KnowledgeSqlSchemaInitializer
{
    public static void EnsureTablesCreated(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[dbo].[rag_documents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[rag_documents] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_rag_documents] PRIMARY KEY,
                    [FileName] nvarchar(255) NOT NULL,
                    [StoredPath] nvarchar(1000) NOT NULL,
                    [Subject] nvarchar(255) NOT NULL,
                    [Chapter] nvarchar(255) NOT NULL,
                    [ContentType] nvarchar(100) NOT NULL,
                    [UploadedAt] datetimeoffset NOT NULL,
                    [ChunkCount] int NOT NULL
                );
                CREATE INDEX [IX_rag_documents_FileName] ON [dbo].[rag_documents] ([FileName]);
            END

            IF OBJECT_ID(N'[dbo].[rag_chunks]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[rag_chunks] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_rag_chunks] PRIMARY KEY,
                    [DocumentId] uniqueidentifier NOT NULL,
                    [FileName] nvarchar(255) NOT NULL,
                    [Subject] nvarchar(255) NOT NULL,
                    [Chapter] nvarchar(255) NOT NULL,
                    [ChunkIndex] int NOT NULL,
                    [Text] nvarchar(max) NOT NULL,
                    [EmbeddingJson] nvarchar(max) NOT NULL,
                    CONSTRAINT [FK_rag_chunks_rag_documents_DocumentId]
                        FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[rag_documents] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_rag_chunks_DocumentId_ChunkIndex] ON [dbo].[rag_chunks] ([DocumentId], [ChunkIndex]);
            END

            IF OBJECT_ID(N'[dbo].[rag_chat_sessions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[rag_chat_sessions] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_rag_chat_sessions] PRIMARY KEY,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL
                );
                CREATE INDEX [IX_rag_chat_sessions_UpdatedAt] ON [dbo].[rag_chat_sessions] ([UpdatedAt]);
            END

            IF OBJECT_ID(N'[dbo].[rag_chat_messages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[rag_chat_messages] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_rag_chat_messages] PRIMARY KEY,
                    [SessionId] uniqueidentifier NOT NULL,
                    [Role] nvarchar(32) NOT NULL,
                    [Content] nvarchar(max) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [FK_rag_chat_messages_rag_chat_sessions_SessionId]
                        FOREIGN KEY ([SessionId]) REFERENCES [dbo].[rag_chat_sessions] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_rag_chat_messages_SessionId_CreatedAt] ON [dbo].[rag_chat_messages] ([SessionId], [CreatedAt]);
            END

            IF OBJECT_ID(N'[dbo].[rag_citations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[rag_citations] (
                    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_rag_citations] PRIMARY KEY,
                    [MessageId] uniqueidentifier NOT NULL,
                    [DocumentId] uniqueidentifier NOT NULL,
                    [FileName] nvarchar(255) NOT NULL,
                    [Subject] nvarchar(255) NOT NULL,
                    [Chapter] nvarchar(255) NOT NULL,
                    [ChunkIndex] int NOT NULL,
                    [Score] float NOT NULL,
                    [Excerpt] nvarchar(max) NOT NULL,
                    CONSTRAINT [FK_rag_citations_rag_chat_messages_MessageId]
                        FOREIGN KEY ([MessageId]) REFERENCES [dbo].[rag_chat_messages] ([Id]) ON DELETE CASCADE
                );
            END
            """);
    }
}
