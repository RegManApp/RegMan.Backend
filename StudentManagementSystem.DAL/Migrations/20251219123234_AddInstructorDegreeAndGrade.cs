using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagementSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorDegreeAndGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                table: "Sections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Degree",
                table: "Instructors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Instructors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionName",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "Degree",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Enrollments");
        }
    }
}
