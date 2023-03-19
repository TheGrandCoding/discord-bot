using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class MediaKeyUpdt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PublishMedia",
                table: "PublishMedia");

            migrationBuilder.AddColumn<uint>(
                name: "Id",
                table: "PublishMedia",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PublishMedia",
                table: "PublishMedia",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PublishMedia_PostId_Platform",
                table: "PublishMedia",
                columns: new[] { "PostId", "Platform" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PublishMedia",
                table: "PublishMedia");

            migrationBuilder.DropIndex(
                name: "IX_PublishMedia_PostId_Platform",
                table: "PublishMedia");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PublishMedia");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PublishMedia",
                table: "PublishMedia",
                columns: new[] { "PostId", "Platform" });
        }
    }
}
