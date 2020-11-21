using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class addArbVote : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArbiterVote",
                columns: table => new
                {
                    VoterId = table.Column<int>(type: "int", nullable: false),
                    VoteeId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArbiterVote", x => new { x.VoterId, x.VoteeId });
                    table.ForeignKey(
                        name: "FK_ArbiterVote_Players_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArbiterVote");
        }
    }
}
