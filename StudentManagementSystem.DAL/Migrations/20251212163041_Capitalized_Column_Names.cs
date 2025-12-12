using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagementSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Capitalized_Column_Names : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "startTime",
                table: "TimeSlots",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "endTime",
                table: "TimeSlots",
                newName: "EndTime");

            migrationBuilder.RenameColumn(
                name: "day",
                table: "TimeSlots",
                newName: "Day");

            migrationBuilder.RenameColumn(
                name: "timeSlotId",
                table: "TimeSlots",
                newName: "TimeSlotId");

            migrationBuilder.RenameColumn(
                name: "scheduleSlotId",
                table: "ScheduleSlots",
                newName: "ScheduleSlotId");

            migrationBuilder.RenameColumn(
                name: "roomNumber",
                table: "Rooms",
                newName: "RoomNumber");

            migrationBuilder.RenameColumn(
                name: "capacity",
                table: "Rooms",
                newName: "Capacity");

            migrationBuilder.RenameColumn(
                name: "building",
                table: "Rooms",
                newName: "Building");

            migrationBuilder.RenameColumn(
                name: "roomId",
                table: "Rooms",
                newName: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "TimeSlots",
                newName: "startTime");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "TimeSlots",
                newName: "endTime");

            migrationBuilder.RenameColumn(
                name: "Day",
                table: "TimeSlots",
                newName: "day");

            migrationBuilder.RenameColumn(
                name: "TimeSlotId",
                table: "TimeSlots",
                newName: "timeSlotId");

            migrationBuilder.RenameColumn(
                name: "ScheduleSlotId",
                table: "ScheduleSlots",
                newName: "scheduleSlotId");

            migrationBuilder.RenameColumn(
                name: "RoomNumber",
                table: "Rooms",
                newName: "roomNumber");

            migrationBuilder.RenameColumn(
                name: "Capacity",
                table: "Rooms",
                newName: "capacity");

            migrationBuilder.RenameColumn(
                name: "Building",
                table: "Rooms",
                newName: "building");

            migrationBuilder.RenameColumn(
                name: "RoomId",
                table: "Rooms",
                newName: "roomId");
        }
    }
}
