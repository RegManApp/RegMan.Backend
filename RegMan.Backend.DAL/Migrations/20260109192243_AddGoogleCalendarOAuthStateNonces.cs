using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegMan.Backend.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarOAuthStateNonces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleCalendarOAuthStateNonces",
                columns: table => new
                {
                    GoogleCalendarOAuthStateNonceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StateHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarOAuthStateNonces", x => x.GoogleCalendarOAuthStateNonceId);
                    table.ForeignKey(
                        name: "FK_GoogleCalendarOAuthStateNonces_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarOAuthStateNonces_StateHash",
                table: "GoogleCalendarOAuthStateNonces",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarOAuthStateNonces_UserId_IsUsed_ExpiresAtUtc",
                table: "GoogleCalendarOAuthStateNonces",
                columns: new[] { "UserId", "IsUsed", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleCalendarOAuthStateNonces");
        }
    }
}
