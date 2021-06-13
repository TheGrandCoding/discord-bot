using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    public partial class MakeTimeKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Threads",
                table: "Threads");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Threads",
                table: "Threads",
                columns: new[] { "_userId", "ThreadId", "LastUpdated" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Threads",
                table: "Threads");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Threads",
                table: "Threads",
                columns: new[] { "_userId", "ThreadId" });
        }
    }
}
