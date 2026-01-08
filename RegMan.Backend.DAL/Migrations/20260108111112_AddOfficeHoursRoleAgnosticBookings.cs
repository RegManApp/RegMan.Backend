using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeHoursRoleAgnosticBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfficeHourBookings_OfficeHourId_StudentId",
                table: "OfficeHourBookings");

            migrationBuilder.RenameColumn(
                name: "StudentNotes",
                table: "OfficeHourBookings",
                newName: "BookerNotes");

            migrationBuilder.RenameColumn(
                name: "InstructorNotes",
                table: "OfficeHourBookings",
                newName: "ProviderNotes");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "OfficeHourBookings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Add as nullable first so we can backfill existing rows safely.
            migrationBuilder.AddColumn<string>(
                name: "BookerRole",
                table: "OfficeHourBookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookerUserId",
                table: "OfficeHourBookings",
                type: "nvarchar(450)",
                nullable: true);

            // Backfill: existing bookings were student bookings.
            migrationBuilder.Sql(@"
UPDATE OHB
SET
    OHB.BookerUserId = S.UserId,
    OHB.BookerRole = 'Student'
FROM OfficeHourBookings OHB
INNER JOIN Students S ON S.StudentId = OHB.StudentId
WHERE OHB.StudentId IS NOT NULL;
");

            // If any bookings can't be backfilled (missing student profile), remove them to avoid breaking NOT NULL/FK.
            migrationBuilder.Sql(@"
DELETE OHB
FROM OfficeHourBookings OHB
WHERE OHB.BookerUserId IS NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "BookerRole",
                table: "OfficeHourBookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookerUserId",
                table: "OfficeHourBookings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourBookings_BookerUserId",
                table: "OfficeHourBookings",
                column: "BookerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourBookings_OfficeHourId_BookerUserId",
                table: "OfficeHourBookings",
                columns: new[] { "OfficeHourId", "BookerUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OfficeHourBookings_AspNetUsers_BookerUserId",
                table: "OfficeHourBookings",
                column: "BookerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfficeHourBookings_AspNetUsers_BookerUserId",
                table: "OfficeHourBookings");

            migrationBuilder.DropIndex(
                name: "IX_OfficeHourBookings_BookerUserId",
                table: "OfficeHourBookings");

            migrationBuilder.DropIndex(
                name: "IX_OfficeHourBookings_OfficeHourId_BookerUserId",
                table: "OfficeHourBookings");

            migrationBuilder.DropColumn(
                name: "BookerRole",
                table: "OfficeHourBookings");

            migrationBuilder.DropColumn(
                name: "BookerUserId",
                table: "OfficeHourBookings");

            migrationBuilder.RenameColumn(
                name: "BookerNotes",
                table: "OfficeHourBookings",
                newName: "StudentNotes");

            migrationBuilder.RenameColumn(
                name: "ProviderNotes",
                table: "OfficeHourBookings",
                newName: "InstructorNotes");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "OfficeHourBookings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHourBookings_OfficeHourId_StudentId",
                table: "OfficeHourBookings",
                columns: new[] { "OfficeHourId", "StudentId" },
                unique: true);
        }
    }
}
