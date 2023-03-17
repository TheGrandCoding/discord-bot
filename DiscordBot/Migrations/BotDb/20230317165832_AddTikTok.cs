using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class AddTikTok : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TikTok_AccessToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TikTok_AccountId",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TikTok_ExpiresAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TikTok_RefreshExpiresAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TikTok_RefreshToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TikTok_AccessToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TikTok_AccountId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TikTok_ExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TikTok_RefreshExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TikTok_RefreshToken",
                table: "Users");
        }
    }
}
