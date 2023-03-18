using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class MoveDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublishMedia_Posts_PostId_Platform",
                table: "PublishMedia");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Posts_Id_DefPlatform",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DefPlatform",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DefaultText",
                table: "Posts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefPlatform",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefaultText",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Posts_Id_DefPlatform",
                table: "Posts",
                columns: new[] { "Id", "DefPlatform" });

            migrationBuilder.AddForeignKey(
                name: "FK_PublishMedia_Posts_PostId_Platform",
                table: "PublishMedia",
                columns: new[] { "PostId", "Platform" },
                principalTable: "Posts",
                principalColumns: new[] { "Id", "DefPlatform" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
