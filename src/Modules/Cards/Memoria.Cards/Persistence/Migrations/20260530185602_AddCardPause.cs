using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Cards.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_paused",
                schema: "cards",
                table: "cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "paused_at_stage",
                schema: "cards",
                table: "cards",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_paused",
                schema: "cards",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "paused_at_stage",
                schema: "cards",
                table: "cards");
        }
    }
}
