using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Reminders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reminders");

            migrationBuilder.CreateTable(
                name: "reminders",
                schema: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_number = table.Column<int>(type: "integer", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hangfire_job_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    message_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_card_id_stage_number",
                schema: "reminders",
                table: "reminders",
                columns: new[] { "card_id", "stage_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_status_scheduled_at",
                schema: "reminders",
                table: "reminders",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_user_id",
                schema: "reminders",
                table: "reminders",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminders",
                schema: "reminders");
        }
    }
}
