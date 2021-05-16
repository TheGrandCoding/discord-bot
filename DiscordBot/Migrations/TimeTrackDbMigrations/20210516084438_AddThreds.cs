using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    public partial class AddThreds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    _userId = table.Column<long>(type: "bigint", nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Comments = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => new { x._userId, x.ThreadId });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Threads");
        }
    }
}
