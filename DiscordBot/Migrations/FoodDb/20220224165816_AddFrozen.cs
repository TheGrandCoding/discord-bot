using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations.FoodDb
{
    public partial class AddFrozen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Frozen",
                table: "Inventory",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frozen",
                table: "Inventory");
        }
    }
}
