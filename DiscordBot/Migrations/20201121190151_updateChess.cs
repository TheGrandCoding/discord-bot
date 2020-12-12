using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class updateChess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "AppealsMotions",
                newName: "MotionType");

            migrationBuilder.AddColumn<int>(
                name: "Losses",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubmitterId",
                table: "AppealsRuling",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldingDate",
                table: "AppealsMotions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "AppealsAttachments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Holding",
                table: "Appeals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppealsWitnesses",
                columns: table => new
                {
                    HearingId = table.Column<int>(type: "int", nullable: false),
                    WitnessId = table.Column<int>(type: "int", nullable: false),
                    ConcludedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealsHearingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsWitnesses", x => new { x.HearingId, x.WitnessId });
                    table.ForeignKey(
                        name: "FK_AppealsWitnesses_Appeals_AppealsHearingId",
                        column: x => x.AppealsHearingId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealsWitnesses_Players_WitnessId",
                        column: x => x.WitnessId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChessDateScore",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChessDateScore", x => new { x.Date, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ChessDateScore_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_TargetId",
                table: "Notes",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_LoserId",
                table: "Games",
                column: "LoserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_WinnerId",
                table: "Games",
                column: "WinnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bans_TargetId",
                table: "Bans",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsRuling_SubmitterId",
                table: "AppealsRuling",
                column: "SubmitterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsMotions_MovantId",
                table: "AppealsMotions",
                column: "MovantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsWitnesses_AppealsHearingId",
                table: "AppealsWitnesses",
                column: "AppealsHearingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsWitnesses_WitnessId",
                table: "AppealsWitnesses",
                column: "WitnessId");

            migrationBuilder.CreateIndex(
                name: "IX_ChessDateScore_PlayerId",
                table: "ChessDateScore",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsMotions_Players_MovantId",
                table: "AppealsMotions",
                column: "MovantId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsRelations_Players_MemberId",
                table: "AppealsRelations",
                column: "MemberId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsRuling_Players_SubmitterId",
                table: "AppealsRuling",
                column: "SubmitterId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Players_TargetId",
                table: "Bans",
                column: "TargetId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games",
                column: "LoserId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games",
                column: "WinnerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Players_TargetId",
                table: "Notes",
                column: "TargetId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppealsMotions_Players_MovantId",
                table: "AppealsMotions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsRelations_Players_MemberId",
                table: "AppealsRelations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsRuling_Players_SubmitterId",
                table: "AppealsRuling");

            migrationBuilder.DropForeignKey(
                name: "FK_Bans_Players_TargetId",
                table: "Bans");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_LoserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_WinnerId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Players_TargetId",
                table: "Notes");

            migrationBuilder.DropTable(
                name: "AppealsWitnesses");

            migrationBuilder.DropTable(
                name: "ChessDateScore");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropIndex(
                name: "IX_Notes_TargetId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Games_LoserId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_WinnerId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Bans_TargetId",
                table: "Bans");

            migrationBuilder.DropIndex(
                name: "IX_AppealsRuling_SubmitterId",
                table: "AppealsRuling");

            migrationBuilder.DropIndex(
                name: "IX_AppealsMotions_MovantId",
                table: "AppealsMotions");

            migrationBuilder.DropColumn(
                name: "Losses",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Wins",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SubmitterId",
                table: "AppealsRuling");

            migrationBuilder.DropColumn(
                name: "HoldingDate",
                table: "AppealsMotions");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "AppealsAttachments");

            migrationBuilder.DropColumn(
                name: "Holding",
                table: "Appeals");

            migrationBuilder.RenameColumn(
                name: "MotionType",
                table: "AppealsMotions",
                newName: "Text");
        }
    }
}
