using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Daryva.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOrganizationMemberRoleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryOwner",
                table: "OrganizationMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Data migration: old model had Owner/Admin/Member/ReadOnly. New model has a single
            // "Landlord" org role plus IsPrimaryOwner. Preserve who was Owner before collapsing
            // every row's Role to Landlord.
            migrationBuilder.Sql(
                "UPDATE \"OrganizationMembers\" SET \"IsPrimaryOwner\" = true WHERE \"Role\" ILIKE 'owner';");
            migrationBuilder.Sql(
                "UPDATE \"OrganizationMembers\" SET \"Role\" = 'Landlord' WHERE \"Role\" <> 'Landlord';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrimaryOwner",
                table: "OrganizationMembers");
        }
    }
}
