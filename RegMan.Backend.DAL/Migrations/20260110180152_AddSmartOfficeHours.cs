using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartOfficeHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create tables if missing (idempotent)
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OfficeHourSessions]
    (
        [SessionId] INT IDENTITY(1,1) NOT NULL,
        [OfficeHourId] INT NOT NULL,
        [ProviderUserId] NVARCHAR(MAX) NOT NULL,
        [Status] INT NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [ClosedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_OfficeHourSessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [FK_OfficeHourSessions_OfficeHours_OfficeHourId] FOREIGN KEY ([OfficeHourId])
            REFERENCES [dbo].[OfficeHours]([OfficeHourId]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OfficeHourQueueEntries]
    (
        [QueueEntryId] INT IDENTITY(1,1) NOT NULL,
        [SessionId] INT NOT NULL,
        [StudentUserId] NVARCHAR(450) NOT NULL,
        [Purpose] NVARCHAR(500) NULL,
        [Status] INT NOT NULL,
        [IsActive] BIT NOT NULL,
        [EnqueuedAtUtc] DATETIME2 NOT NULL,
        [ReadyAtUtc] DATETIME2 NULL,
        [InProgressAtUtc] DATETIME2 NULL,
        [DoneAtUtc] DATETIME2 NULL,
        [NoShowAtUtc] DATETIME2 NULL,
        [ReadyExpiresAtUtc] DATETIME2 NULL,
        [LastStateChangedByUserId] NVARCHAR(MAX) NULL,
        [LastStateChangedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_OfficeHourQueueEntries] PRIMARY KEY ([QueueEntryId]),
        CONSTRAINT [FK_OfficeHourQueueEntries_AspNetUsers_StudentUserId] FOREIGN KEY ([StudentUserId])
            REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfficeHourQueueEntries_OfficeHourSessions_SessionId] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[OfficeHourSessions]([SessionId]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQrTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OfficeHourQrTokens]
    (
        [QrTokenId] INT IDENTITY(1,1) NOT NULL,
        [QueueEntryId] INT NOT NULL,
        [CurrentNonce] UNIQUEIDENTIFIER NULL,
        [IssuedAtUtc] DATETIME2 NULL,
        [ExpiresAtUtc] DATETIME2 NULL,
        [UsedAtUtc] DATETIME2 NULL,
        [UsedByUserId] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_OfficeHourQrTokens] PRIMARY KEY ([QrTokenId]),
        CONSTRAINT [FK_OfficeHourQrTokens_OfficeHourQueueEntries_QueueEntryId] FOREIGN KEY ([QueueEntryId])
            REFERENCES [dbo].[OfficeHourQueueEntries]([QueueEntryId]) ON DELETE NO ACTION
    );
END;
");

            // Align required columns for existing tables (idempotent)
            migrationBuilder.Sql(@"
-- OfficeHourQueueEntries: add missing columns used by the model
IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'SessionId') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [SessionId] INT NOT NULL CONSTRAINT [DF_OfficeHourQueueEntries_SessionId] DEFAULT (0);
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'Status') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [Status] INT NOT NULL CONSTRAINT [DF_OfficeHourQueueEntries_Status] DEFAULT (0);
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'EnqueuedAtUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [EnqueuedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_OfficeHourQueueEntries_EnqueuedAtUtc] DEFAULT (SYSUTCDATETIME());
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'LastStateChangedAtUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [LastStateChangedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_OfficeHourQueueEntries_LastStateChangedAtUtc] DEFAULT (SYSUTCDATETIME());
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'IsActive') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [IsActive] BIT NOT NULL CONSTRAINT [DF_OfficeHourQueueEntries_IsActive] DEFAULT (1);
END;
");

            // Create indexes if missing (idempotent). Use dynamic SQL to avoid compile-time column resolution issues.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourSessions', N'OfficeHourId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourSessions_OfficeHourId' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourSessions]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OfficeHourSessions_OfficeHourId] ON [dbo].[OfficeHourSessions] ([OfficeHourId]);');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'SessionId') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'EnqueuedAtUtc') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourQueueEntries_SessionId_EnqueuedAtUtc' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_OfficeHourQueueEntries_SessionId_EnqueuedAtUtc] ON [dbo].[OfficeHourQueueEntries] ([SessionId], [EnqueuedAtUtc]);');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'StudentUserId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourQueueEntries_StudentUserId' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_OfficeHourQueueEntries_StudentUserId] ON [dbo].[OfficeHourQueueEntries] ([StudentUserId]);');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'SessionId') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'StudentUserId') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'IsActive') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourQueueEntries_SessionId_StudentUserId_IsActive' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OfficeHourQueueEntries_SessionId_StudentUserId_IsActive] ON [dbo].[OfficeHourQueueEntries] ([SessionId], [StudentUserId], [IsActive]) WHERE [IsActive] = 1;');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQrTokens]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQrTokens', N'QueueEntryId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourQrTokens_QueueEntryId' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourQrTokens]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OfficeHourQrTokens_QueueEntryId] ON [dbo].[OfficeHourQrTokens] ([QueueEntryId]);');
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficeHourQrTokens");

            migrationBuilder.DropTable(
                name: "OfficeHourQueueEntries");

            migrationBuilder.DropTable(
                name: "OfficeHourSessions");
        }
    }
}
