using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class addForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OperatorId",
                table: "Notes",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Bans_OperatorId",
                table: "Bans",
                column: "OperatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Players_OperatorId",
                table: "Bans",
                column: "OperatorId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games",
                column: "LoserId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games",
                column: "WinnerId",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Players_OperatorId",
                table: "Notes",
                column: "OperatorId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bans_Players_OperatorId",
                table: "Bans");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Players_OperatorId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_OperatorId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Bans_OperatorId",
                table: "Bans");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games",
                column: "LoserId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games",
                column: "WinnerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
