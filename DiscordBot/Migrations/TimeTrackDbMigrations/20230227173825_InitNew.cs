using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    /// <inheritdoc />
    public partial class InitNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ignores",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    VideoId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ignores", x => new { x.UserId, x.VideoId });
                });

            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => new { x.UserId, x.ThreadId, x.LastUpdated });
                });

            migrationBuilder.CreateTable(
                name: "WatchTimes",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    VideoId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WatchedTime = table.Column<double>(type: "float", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchTimes", x => new { x.UserId, x.VideoId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ignores");

            migrationBuilder.DropTable(
                name: "Threads");

            migrationBuilder.DropTable(
                name: "WatchTimes");
        }
    }
}
