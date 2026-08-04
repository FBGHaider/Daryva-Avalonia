using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Daryva.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportAccessCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportAccessCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessCodes_Code",
                table: "SupportAccessCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportAccessCodes_OrganizationId",
                table: "SupportAccessCodes",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportAccessCodes");
        }
    }
}
