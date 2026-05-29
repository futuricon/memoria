using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Reviews.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_feedback",
                schema: "reviews",
                table: "reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ai_score",
                schema: "reviews",
                table: "reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "answer_text",
                schema: "reviews",
                table: "reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "auto_graded",
                schema: "reviews",
                table: "reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_feedback",
                schema: "reviews",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_score",
                schema: "reviews",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "answer_text",
                schema: "reviews",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "auto_graded",
                schema: "reviews",
                table: "reviews");
        }
    }
}
