using Microsoft.EntityFrameworkCore.Migrations;

namespace DiscordBot.Migrations
{
    public partial class motionFile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppealsMotionFiles_AppealsAttachments_AttachmentId",
                table: "AppealsMotionFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsMotionFiles_AppealsMotions_MotionId",
                table: "AppealsMotionFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppealsMotionFiles",
                table: "AppealsMotionFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppealsMotionFiles_MotionId",
                table: "AppealsMotionFiles");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "AppealsMotionFiles");

            migrationBuilder.AlterColumn<int>(
                name: "MotionId",
                table: "AppealsMotionFiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AttachmentId",
                table: "AppealsMotionFiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppealsMotionFiles",
                table: "AppealsMotionFiles",
                columns: new[] { "MotionId", "AttachmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsMotionFiles_AppealsAttachments_AttachmentId",
                table: "AppealsMotionFiles",
                column: "AttachmentId",
                principalTable: "AppealsAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsMotionFiles_AppealsMotions_MotionId",
                table: "AppealsMotionFiles",
                column: "MotionId",
                principalTable: "AppealsMotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppealsMotionFiles_AppealsAttachments_AttachmentId",
                table: "AppealsMotionFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_AppealsMotionFiles_AppealsMotions_MotionId",
                table: "AppealsMotionFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppealsMotionFiles",
                table: "AppealsMotionFiles");

            migrationBuilder.AlterColumn<int>(
                name: "AttachmentId",
                table: "AppealsMotionFiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MotionId",
                table: "AppealsMotionFiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "AppealsMotionFiles",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppealsMotionFiles",
                table: "AppealsMotionFiles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AppealsMotionFiles_MotionId",
                table: "AppealsMotionFiles",
                column: "MotionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsMotionFiles_AppealsAttachments_AttachmentId",
                table: "AppealsMotionFiles",
                column: "AttachmentId",
                principalTable: "AppealsAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealsMotionFiles_AppealsMotions_MotionId",
                table: "AppealsMotionFiles",
                column: "MotionId",
                principalTable: "AppealsMotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
