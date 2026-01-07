using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarAuditEntries",
                columns: table => new
                {
                    CalendarAuditEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarAuditEntries", x => x.CalendarAuditEntryId);
                });

            migrationBuilder.CreateTable(
                name: "GoogleCalendarEventLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceEntityId = table.Column<int>(type: "int", nullable: false),
                    GoogleCalendarId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarEventLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleCalendarEventLinks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledNotifications",
                columns: table => new
                {
                    ScheduledNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceEntityId = table.Column<int>(type: "int", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledNotifications", x => x.ScheduledNotificationId);
                });

            migrationBuilder.CreateTable(
                name: "UserCalendarPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WeekStartDay = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HideWeekends = table.Column<bool>(type: "bit", nullable: false),
                    DefaultReminderMinutes = table.Column<int>(type: "int", nullable: true),
                    EventTypeColorMapJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCalendarPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserCalendarPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserReminderRules",
                columns: table => new
                {
                    RuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    MinutesBefore = table.Column<int>(type: "int", nullable: false),
                    ChannelMask = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReminderRules", x => x.RuleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarEventLinks_UserId_SourceEntityType_SourceEntityId",
                table: "GoogleCalendarEventLinks",
                columns: new[] { "UserId", "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_UserId_TriggerType_SourceEntityType_SourceEntityId_ScheduledAtUtc",
                table: "ScheduledNotifications",
                columns: new[] { "UserId", "TriggerType", "SourceEntityType", "SourceEntityId", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarAuditEntries");

            migrationBuilder.DropTable(
                name: "GoogleCalendarEventLinks");

            migrationBuilder.DropTable(
                name: "ScheduledNotifications");

            migrationBuilder.DropTable(
                name: "UserCalendarPreferences");

            migrationBuilder.DropTable(
                name: "UserReminderRules");
        }
    }
}
