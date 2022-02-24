using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations.FoodDb
{
    public partial class AddExt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreezingExtends",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreezingExtends",
                table: "Products");
        }
    }
}
