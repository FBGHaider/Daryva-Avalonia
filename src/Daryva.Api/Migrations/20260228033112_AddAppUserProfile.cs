using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Daryva.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                table: "Houses",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppUserProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepositReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AmountReturned = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepositReturns_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepositReturns_Tenancies_TenancyId",
                        column: x => x.TenancyId,
                        principalTable: "Tenancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserProfiles_Email",
                table: "AppUserProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DepositReturns_OrganizationId",
                table: "DepositReturns",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositReturns_TenancyId",
                table: "DepositReturns",
                column: "TenancyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserProfiles");

            migrationBuilder.DropTable(
                name: "DepositReturns");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                table: "Houses",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
