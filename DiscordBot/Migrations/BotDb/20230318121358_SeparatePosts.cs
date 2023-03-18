using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class SeparatePosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultMediaUrl",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Discord_Caption",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Discord_Kind",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Discord_MediaUrl",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Instagram_Caption",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Instagram_Kind",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Instagram_MediaUrl",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Instagram_OriginalId",
                table: "Posts");

            migrationBuilder.AddColumn<int>(
                name: "DefPlatform",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Posts_Id_DefPlatform",
                table: "Posts",
                columns: new[] { "Id", "DefPlatform" });

            migrationBuilder.CreateTable(
                name: "PostPlatforms",
                columns: table => new
                {
                    PostId = table.Column<uint>(type: "int unsigned", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OriginalId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostPlatforms", x => new { x.PostId, x.Platform });
                    table.ForeignKey(
                        name: "FK_PostPlatforms_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PublishMedia",
                columns: table => new
                {
                    PostId = table.Column<uint>(type: "int unsigned", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RemoteUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LocalPath = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishMedia", x => new { x.PostId, x.Platform });
                    table.ForeignKey(
                        name: "FK_PublishMedia_PostPlatforms_PostId_Platform",
                        columns: x => new { x.PostId, x.Platform },
                        principalTable: "PostPlatforms",
                        principalColumns: new[] { "PostId", "Platform" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublishMedia_Posts_PostId_Platform",
                        columns: x => new { x.PostId, x.Platform },
                        principalTable: "Posts",
                        principalColumns: new[] { "Id", "DefPlatform" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublishMedia");

            migrationBuilder.DropTable(
                name: "PostPlatforms");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Posts_Id_DefPlatform",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DefPlatform",
                table: "Posts");

            migrationBuilder.AddColumn<string>(
                name: "DefaultMediaUrl",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Discord_Caption",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Discord_Kind",
                table: "Posts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discord_MediaUrl",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Instagram_Caption",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Instagram_Kind",
                table: "Posts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instagram_MediaUrl",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Instagram_OriginalId",
                table: "Posts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
