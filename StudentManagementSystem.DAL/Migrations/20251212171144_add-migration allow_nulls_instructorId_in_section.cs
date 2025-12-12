using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagementSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class addmigrationallow_nulls_instructorId_in_section : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Instructors_InstructorId",
                table: "Sections");

            migrationBuilder.AlterColumn<int>(
                name: "InstructorId",
                table: "Sections",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Instructors_InstructorId",
                table: "Sections",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "InstructorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Instructors_InstructorId",
                table: "Sections");

            migrationBuilder.AlterColumn<int>(
                name: "InstructorId",
                table: "Sections",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Instructors_InstructorId",
                table: "Sections",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "InstructorId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
