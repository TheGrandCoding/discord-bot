using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class addChess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sealed = table.Column<bool>(type: "bit", nullable: false),
                    IsArbiterCase = table.Column<bool>(type: "bit", nullable: false),
                    AppealOf = table.Column<int>(type: "int", nullable: true),
                    Filed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Commenced = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Concluded = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppealsAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Filed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiledBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorId = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    GivenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WinnerId = table.Column<int>(type: "int", nullable: false),
                    LoserId = table.Column<int>(type: "int", nullable: false),
                    Draw = table.Column<bool>(type: "bit", nullable: false),
                    WinnerChange = table.Column<int>(type: "int", nullable: false),
                    LoserChange = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovalNeeded = table.Column<int>(type: "int", nullable: false),
                    ApprovalGiven = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorId = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    GivenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresInDays = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    Removed = table.Column<bool>(type: "bit", nullable: false),
                    RequireTiming = table.Column<bool>(type: "bit", nullable: false),
                    RequireGameApproval = table.Column<bool>(type: "bit", nullable: false),
                    WithdrawnModVote = table.Column<bool>(type: "bit", nullable: false),
                    DiscordAccount = table.Column<long>(type: "bigint", nullable: false),
                    DateLastPresent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsBuiltInAccount = table.Column<bool>(type: "bit", nullable: false),
                    DismissalReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Modifier = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppealsMotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HearingId = table.Column<int>(type: "int", nullable: false),
                    Filed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MovantId = table.Column<int>(type: "int", nullable: false),
                    Holding = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsMotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealsMotions_Appeals_HearingId",
                        column: x => x.HearingId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppealsRelations",
                columns: table => new
                {
                    AppealHearingId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    Relation = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsRelations", x => new { x.MemberId, x.AppealHearingId });
                    table.ForeignKey(
                        name: "FK_AppealsRelations_Appeals_AppealHearingId",
                        column: x => x.AppealHearingId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppealsExhibits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HearingId = table.Column<int>(type: "int", nullable: true),
                    AttachmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsExhibits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealsExhibits_Appeals_HearingId",
                        column: x => x.HearingId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealsExhibits_AppealsAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "AppealsAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealsRuling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HearingId = table.Column<int>(type: "int", nullable: false),
                    Holding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsRuling", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealsRuling_Appeals_HearingId",
                        column: x => x.HearingId,
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealsRuling_AppealsAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "AppealsAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealsMotionFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MotionId = table.Column<int>(type: "int", nullable: true),
                    AttachmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealsMotionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealsMotionFiles_AppealsAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "AppealsAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealsMotionFiles_AppealsMotions_MotionId",
                        column: x => x.MotionId,
                        principalTable: "AppealsMotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealsExhibits_AttachmentId",
                table: "AppealsExhibits",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsExhibits_HearingId",
                table: "AppealsExhibits",
                column: "HearingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsMotionFiles_AttachmentId",
                table: "AppealsMotionFiles",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsMotionFiles_MotionId",
                table: "AppealsMotionFiles",
                column: "MotionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsMotions_HearingId",
                table: "AppealsMotions",
                column: "HearingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsRelations_AppealHearingId",
                table: "AppealsRelations",
                column: "AppealHearingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsRuling_AttachmentId",
                table: "AppealsRuling",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsRuling_HearingId",
                table: "AppealsRuling",
                column: "HearingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Id_WinnerId_LoserId",
                table: "Games",
                columns: new[] { "Id", "WinnerId", "LoserId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealsExhibits");

            migrationBuilder.DropTable(
                name: "AppealsMotionFiles");

            migrationBuilder.DropTable(
                name: "AppealsRelations");

            migrationBuilder.DropTable(
                name: "AppealsRuling");

            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "AppealsMotions");

            migrationBuilder.DropTable(
                name: "AppealsAttachments");

            migrationBuilder.DropTable(
                name: "Appeals");
        }
    }
}
