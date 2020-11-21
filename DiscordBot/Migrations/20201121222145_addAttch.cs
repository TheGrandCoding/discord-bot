using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class addAttch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppealsExhibits_Appeals_HearingId",
                table: "AppealsExhibits");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsExhibits_AppealsAttachments_AttachmentId",
                table: "AppealsExhibits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppealsExhibits",
                table: "AppealsExhibits");

            migrationBuilder.DropIndex(
                name: "IX_AppealsExhibits_HearingId",
                table: "AppealsExhibits");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "AppealsExhibits");

            migrationBuilder.AlterColumn<int>(
                name: "HearingId",
                table: "AppealsExhibits",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AttachmentId",
                table: "AppealsExhibits",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppealsExhibits",
                table: "AppealsExhibits",
                columns: new[] { "HearingId", "AttachmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsExhibits_Appeals_HearingId",
                table: "AppealsExhibits",
                column: "HearingId",
                principalTable: "Appeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsExhibits_AppealsAttachments_AttachmentId",
                table: "AppealsExhibits",
                column: "AttachmentId",
                principalTable: "AppealsAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppealsExhibits_Appeals_HearingId",
                table: "AppealsExhibits");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsExhibits_AppealsAttachments_AttachmentId",
                table: "AppealsExhibits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppealsExhibits",
                table: "AppealsExhibits");

            migrationBuilder.AlterColumn<int>(
                name: "AttachmentId",
                table: "AppealsExhibits",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "HearingId",
                table: "AppealsExhibits",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "AppealsExhibits",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppealsExhibits",
                table: "AppealsExhibits",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsExhibits_HearingId",
                table: "AppealsExhibits",
                column: "HearingId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsExhibits_Appeals_HearingId",
                table: "AppealsExhibits",
                column: "HearingId",
                principalTable: "Appeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsExhibits_AppealsAttachments_AttachmentId",
                table: "AppealsExhibits",
                column: "AttachmentId",
                principalTable: "AppealsAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
