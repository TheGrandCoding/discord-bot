using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.HoursDb
{
    public partial class InitHours : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entries",
                columns: table => new
                {
                    SettingId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NormalHours = table.Column<double>(type: "float", nullable: false),
                    OvertimeHours = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => new { x.SettingId, x.UserId, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedStartTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedEndTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalRate = table.Column<double>(type: "float", nullable: false),
                    OvertimeRate = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => new { x.Id, x.UserId });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entries");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
