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
            migrationBuilder.CreateTable(
                name: "OfficeHourSessions",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfficeHourId = table.Column<int>(type: "int", nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeHourSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_OfficeHourSessions_OfficeHours_OfficeHourId",
                        column: x => x.OfficeHourId,
                        principalTable: "OfficeHours",
                        principalColumn: "OfficeHourId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeHourQueueEntries",
                columns: table => new
                {
                    QueueEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    StudentUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadyAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InProgressAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DoneAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NoShowAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadyExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStateChangedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastStateChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeHourQueueEntries", x => x.QueueEntryId);
                    table.ForeignKey(
                        name: "FK_OfficeHourQueueEntries_AspNetUsers_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeHourQueueEntries_OfficeHourSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "OfficeHourSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeHourQrTokens",
                columns: table => new
                {
                    QrTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QueueEntryId = table.Column<int>(type: "int", nullable: false),
                    CurrentNonce = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeHourQrTokens", x => x.QrTokenId);
                    table.ForeignKey(
                        name: "FK_OfficeHourQrTokens_OfficeHourQueueEntries_QueueEntryId",
                        column: x => x.QueueEntryId,
                        principalTable: "OfficeHourQueueEntries",
                        principalColumn: "QueueEntryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourQrTokens_QueueEntryId",
                table: "OfficeHourQrTokens",
                column: "QueueEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourQueueEntries_SessionId_EnqueuedAtUtc",
                table: "OfficeHourQueueEntries",
                columns: new[] { "SessionId", "EnqueuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourQueueEntries_SessionId_StudentUserId_IsActive",
                table: "OfficeHourQueueEntries",
                columns: new[] { "SessionId", "StudentUserId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourQueueEntries_StudentUserId",
                table: "OfficeHourQueueEntries",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourSessions_OfficeHourId",
                table: "OfficeHourSessions",
                column: "OfficeHourId",
                unique: true);
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
