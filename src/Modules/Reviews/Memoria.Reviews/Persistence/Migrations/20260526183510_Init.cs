using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Reviews.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reviews");

            migrationBuilder.CreateTable(
                name: "reviews",
                schema: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rating = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    card_title_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_card_id",
                schema: "reviews",
                table: "reviews",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_reminder_id",
                schema: "reviews",
                table: "reviews",
                column: "reminder_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id_reviewed_at",
                schema: "reviews",
                table: "reviews",
                columns: new[] { "user_id", "reviewed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews",
                schema: "reviews");
        }
    }
}
