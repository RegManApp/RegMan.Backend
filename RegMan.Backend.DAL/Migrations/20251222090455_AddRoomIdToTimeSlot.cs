using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomIdToTimeSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "TimeSlots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_RoomId",
                table: "TimeSlots",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeSlots_Rooms_RoomId",
                table: "TimeSlots",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeSlots_Rooms_RoomId",
                table: "TimeSlots");

            migrationBuilder.DropIndex(
                name: "IX_TimeSlots_RoomId",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "TimeSlots");
        }
    }
}
