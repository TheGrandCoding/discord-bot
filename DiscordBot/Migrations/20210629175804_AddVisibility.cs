using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class AddVisibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Public",
                table: "Events");

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Events");

            migrationBuilder.AddColumn<bool>(
                name: "Public",
                table: "Events",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
