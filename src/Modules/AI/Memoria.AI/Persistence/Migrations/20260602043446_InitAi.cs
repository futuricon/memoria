using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.AI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "ai_usage",
                schema: "ai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    is_failure = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_occurred_at",
                schema: "ai",
                table: "ai_usage",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_user_id_occurred_at",
                schema: "ai",
                table: "ai_usage",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage",
                schema: "ai");
        }
    }
}
