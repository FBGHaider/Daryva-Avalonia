using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Daryva.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetSentAt",
                table: "AppUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresAt",
                table: "AppUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "AppUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetSentAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                table: "AppUsers");
        }
    }
}
