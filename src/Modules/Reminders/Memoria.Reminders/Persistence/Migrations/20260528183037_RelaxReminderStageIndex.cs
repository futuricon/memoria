using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Reminders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxReminderStageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reminders_card_id_stage_number",
                schema: "reminders",
                table: "reminders");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_card_id_stage_number",
                schema: "reminders",
                table: "reminders",
                columns: new[] { "card_id", "stage_number" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_card_id_status",
                schema: "reminders",
                table: "reminders",
                columns: new[] { "card_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reminders_card_id_stage_number",
                schema: "reminders",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "ix_reminders_card_id_status",
                schema: "reminders",
                table: "reminders");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_card_id_stage_number",
                schema: "reminders",
                table: "reminders",
                columns: new[] { "card_id", "stage_number" },
                unique: true);
        }
    }
}
