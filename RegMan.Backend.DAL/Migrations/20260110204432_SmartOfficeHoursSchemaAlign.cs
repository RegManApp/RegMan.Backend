using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SmartOfficeHoursSchemaAlign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Idempotent schema alignment for Smart Office Hours against existing production tables.

-- OfficeHourQueueEntries: add missing column used for Ready->NoShow handling
IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'ReadyExpiresAtUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourQueueEntries]
        ADD [ReadyExpiresAtUtc] DATETIME2 NULL;
END;

-- OfficeHourCompletionQrTokens: add missing rotation state column
IF OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourCompletionQrTokens', N'CurrentNonce') IS NULL
BEGIN
    ALTER TABLE [dbo].[OfficeHourCompletionQrTokens]
        ADD [CurrentNonce] UNIQUEIDENTIFIER NULL;
END;

-- Ensure TokenHash has a default generator (required by existing schema)
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

-- Ensure key supporting indexes exist (guarded)
IF OBJECT_ID(N'[dbo].[OfficeHourSessions]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourSessions', N'OfficeHourId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourSessions_OfficeHourId' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourSessions]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_OfficeHourSessions_OfficeHourId] ON [dbo].[OfficeHourSessions] ([OfficeHourId]);');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourQueueEntries', N'StudentUserId') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourQueueEntries_StudentUserId' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourQueueEntries]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_OfficeHourQueueEntries_StudentUserId] ON [dbo].[OfficeHourQueueEntries] ([StudentUserId]);');
END;

IF OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.OfficeHourCompletionQrTokens', N'TokenHash') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfficeHourCompletionQrTokens_TokenHash' AND object_id = OBJECT_ID(N'[dbo].[OfficeHourCompletionQrTokens]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OfficeHourCompletionQrTokens_TokenHash] ON [dbo].[OfficeHourCompletionQrTokens] ([TokenHash]);');
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
