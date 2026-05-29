using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Cards.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "cards",
                table: "cards",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Note");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                schema: "cards",
                table: "cards");
        }
    }
}
