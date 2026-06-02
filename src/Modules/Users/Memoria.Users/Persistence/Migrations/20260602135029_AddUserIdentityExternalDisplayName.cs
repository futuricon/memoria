using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentityExternalDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_display_name",
                schema: "users",
                table: "user_identities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "external_display_name",
                schema: "users",
                table: "user_identities");
        }
    }
}
