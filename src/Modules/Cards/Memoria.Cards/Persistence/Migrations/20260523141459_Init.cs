using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Cards.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cards");

            migrationBuilder.CreateTable(
                name: "cards",
                schema: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "card_tags",
                schema: "cards",
                columns: table => new
                {
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_tags", x => new { x.card_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_card_tags_cards_card_id",
                        column: x => x.card_id,
                        principalSchema: "cards",
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_card_tags_tag_id",
                schema: "cards",
                table: "card_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_deleted_at",
                schema: "cards",
                table: "cards",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_cards_user_id",
                schema: "cards",
                table: "cards",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_user_id_created_at",
                schema: "cards",
                table: "cards",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tags_user_id_normalized_name",
                schema: "cards",
                table: "tags",
                columns: new[] { "user_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_tags",
                schema: "cards");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "cards");

            migrationBuilder.DropTable(
                name: "cards",
                schema: "cards");
        }
    }
}
