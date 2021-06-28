using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class AddRecurSeries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecursOn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartRecur = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndRecur = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_SeriesId",
                table: "Events",
                column: "SeriesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Series_SeriesId",
                table: "Events",
                column: "SeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Series_SeriesId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Events_SeriesId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Events");
        }
    }
}
