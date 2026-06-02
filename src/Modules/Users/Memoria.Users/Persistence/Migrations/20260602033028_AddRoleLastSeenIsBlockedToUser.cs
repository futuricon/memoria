using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleLastSeenIsBlockedToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                schema: "users",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_at",
                schema: "users",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "users",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_blocked",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_seen_at",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "users",
                table: "users");
        }
    }
}
