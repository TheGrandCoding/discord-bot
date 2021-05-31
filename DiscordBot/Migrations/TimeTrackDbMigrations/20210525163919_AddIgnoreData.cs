using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    public partial class AddIgnoreData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ignores",
                columns: table => new
                {
                    _userId = table.Column<long>(type: "bigint", nullable: false),
                    VideoId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ignores", x => new { x._userId, x.VideoId });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ignores");
        }
    }
}
