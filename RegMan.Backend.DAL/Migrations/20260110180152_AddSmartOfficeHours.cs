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
            migrationBuilder.Sql(@"
-- NOTE: These tables already exist in production. Do NOT create them.

-- OfficeHourQueueEntries: add missing column used by Smart Office Hours (idempotent)
IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'ReadyExpiresAtUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [ReadyExpiresAtUtc] DATETIME2 NULL;
END;

-- OfficeHourCompletionQrTokens: add missing rotation state column (idempotent)
IF OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourCompletionQrTokens', N'CurrentNonce') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourCompletionQrTokens]
        ADD [CurrentNonce] UNIQUEIDENTIFIER NULL;
END;

-- Ensure TokenHash has a DB-side generator (unique index exists; avoid NULL inserts)
IF OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourCompletionQrTokens', N'TokenHash') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID(N'dbo.OfficeHourCompletionQrTokens')
      AND c.name = N'TokenHash'
)
BEGIN
    ALTER TABLE [dbo].[OfficeHourCompletionQrTokens]
        ADD CONSTRAINT [DF_OfficeHourCompletionQrTokens_TokenHash]
        DEFAULT (CONVERT(nvarchar(450), NEWID())) FOR [TokenHash];
END;

-- Foreign keys: only add if missing, referencing real PKs/columns
IF OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[OfficeHours]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourSessions', N'OfficeHourId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OfficeHourSessions_OfficeHours_OfficeHourId')
BEGIN
    ALTER TABLE [dbo].[OfficeHourSessions]
        ADD CONSTRAINT [FK_OfficeHourSessions_OfficeHours_OfficeHourId]
        FOREIGN KEY ([OfficeHourId]) REFERENCES [dbo].[OfficeHours]([OfficeHourId]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourSessions', N'ProviderUserId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OfficeHourSessions_AspNetUsers_ProviderUserId')
BEGIN
    ALTER TABLE [dbo].[OfficeHourSessions]
        ADD CONSTRAINT [FK_OfficeHourSessions_AspNetUsers_ProviderUserId]
        FOREIGN KEY ([ProviderUserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'OfficeHourSessionId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OfficeHourQueueEntries_OfficeHourSessions_OfficeHourSessionId')
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD CONSTRAINT [FK_OfficeHourQueueEntries_OfficeHourSessions_OfficeHourSessionId]
        FOREIGN KEY ([OfficeHourSessionId]) REFERENCES [dbo].[OfficeHourSessions]([OfficeHourSessionId]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'StudentUserId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OfficeHourQueueEntries_AspNetUsers_StudentUserId')
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD CONSTRAINT [FK_OfficeHourQueueEntries_AspNetUsers_StudentUserId]
        FOREIGN KEY ([StudentUserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE NO ACTION;
END;

IF OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]', N'U') IS NOT NULL
AND OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourCompletionQrTokens', N'OfficeHourQueueEntryId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OfficeHourCompletionQrTokens_OfficeHourQueueEntries_OfficeHourQueueEntryId')
BEGIN
    ALTER TABLE [dbo].[OfficeHourCompletionQrTokens]
        ADD CONSTRAINT [FK_OfficeHourCompletionQrTokens_OfficeHourQueueEntries_OfficeHourQueueEntryId]
        FOREIGN KEY ([OfficeHourQueueEntryId]) REFERENCES [dbo].[OfficeHourQueueEntries]([OfficeHourQueueEntryId]) ON DELETE NO ACTION;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: production-safe migration (no drops).
        }
    }
}
