using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagementSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class OfficeHourBookingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfficeHours_TimeSlots_TimeSlotId",
                table: "OfficeHours");

            migrationBuilder.DropIndex(
                name: "IX_OfficeHours_TimeSlotId",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "TimeSlotId",
                table: "OfficeHours");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OfficeHours",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "OfficeHours",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "OfficeHours",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "OfficeHours",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "OfficeHours",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurringDay",
                table: "OfficeHours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "OfficeHours",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OfficeHours",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OfficeHours",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfficeHourBookings",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfficeHourId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StudentNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InstructorNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeHourBookings", x => x.BookingId);
                    table.ForeignKey(
                        name: "FK_OfficeHourBookings_OfficeHours_OfficeHourId",
                        column: x => x.OfficeHourId,
                        principalTable: "OfficeHours",
                        principalColumn: "OfficeHourId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfficeHourBookings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourBookings_OfficeHourId_StudentId",
                table: "OfficeHourBookings",
                columns: new[] { "OfficeHourId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourBookings_StudentId",
                table: "OfficeHourBookings",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OfficeHourBookings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "RecurringDay",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OfficeHours");

            migrationBuilder.AddColumn<int>(
                name: "TimeSlotId",
                table: "OfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHours_TimeSlotId",
                table: "OfficeHours",
                column: "TimeSlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_OfficeHours_TimeSlots_TimeSlotId",
                table: "OfficeHours",
                column: "TimeSlotId",
                principalTable: "TimeSlots",
                principalColumn: "TimeSlotId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
