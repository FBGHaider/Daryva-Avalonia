using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Daryva.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAppUserLinkAndInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppUserId",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteSentAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteTokenExpiresAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteTokenHash",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_AppUserId",
                table: "Tenants",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_InviteTokenHash",
                table: "Tenants",
                column: "InviteTokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_AppUsers_AppUserId",
                table: "Tenants",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_AppUsers_AppUserId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_AppUserId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_InviteTokenHash",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InviteSentAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InviteTokenExpiresAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InviteTokenHash",
                table: "Tenants");
        }
    }
}
