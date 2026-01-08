using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeHoursRoleAgnosticOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfficeHours_Instructors_InstructorId",
                table: "OfficeHours");

            migrationBuilder.AlterColumn<int>(
                name: "InstructorId",
                table: "OfficeHours",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "OfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OwnerRole",
                table: "OfficeHours",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "OfficeHours",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE OH
SET
    OH.OwnerUserId = I.UserId,
    OH.OwnerRole = 'Instructor'
FROM OfficeHours OH
INNER JOIN Instructors I ON OH.InstructorId = I.InstructorId
WHERE (OH.OwnerUserId IS NULL OR OH.OwnerUserId = '')
");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerRole",
                table: "OfficeHours",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerUserId",
                table: "OfficeHours",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeHours_OwnerUserId",
                table: "OfficeHours",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OfficeHours_AspNetUsers_OwnerUserId",
                table: "OfficeHours",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OfficeHours_Instructors_InstructorId",
                table: "OfficeHours",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "InstructorId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfficeHours_AspNetUsers_OwnerUserId",
                table: "OfficeHours");

            migrationBuilder.DropForeignKey(
                name: "FK_OfficeHours_Instructors_InstructorId",
                table: "OfficeHours");

            migrationBuilder.DropIndex(
                name: "IX_OfficeHours_OwnerUserId",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "OwnerRole",
                table: "OfficeHours");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "OfficeHours");

            migrationBuilder.AlterColumn<int>(
                name: "InstructorId",
                table: "OfficeHours",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OfficeHours_Instructors_InstructorId",
                table: "OfficeHours",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "InstructorId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
