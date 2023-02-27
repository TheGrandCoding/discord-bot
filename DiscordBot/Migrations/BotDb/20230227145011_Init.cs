using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Approved = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    Verified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RedirectUrl = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnectionsPasswordHash = table.Column<string>(name: "Connections_PasswordHash", type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnectionsDiscordId = table.Column<string>(name: "Connections_DiscordId", type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionsPairedVoiceChannels = table.Column<int>(name: "Options_PairedVoiceChannels", type: "int", nullable: false),
                    OptionsWhenToNotifyIsolation = table.Column<int>(name: "Options_WhenToNotifyIsolation", type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Token = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IP = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Approved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UserId = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Token);
                    table.ForeignKey(
                        name: "FK_AuthSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuthTokens",
                columns: table => new
                {
                    Token = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthTokens", x => x.Token);
                    table.ForeignKey(
                        name: "FK_AuthTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BotDbApprovedIP",
                columns: table => new
                {
                    UserId = table.Column<uint>(type: "int unsigned", nullable: false),
                    IP = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotDbApprovedIP", x => new { x.UserId, x.IP });
                    table.ForeignKey(
                        name: "FK_BotDbApprovedIP_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BotDbPermission",
                columns: table => new
                {
                    UserId = table.Column<uint>(type: "int unsigned", nullable: false),
                    PermNode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotDbPermission", x => new { x.UserId, x.PermNode });
                    table.ForeignKey(
                        name: "FK_BotDbPermission_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId",
                table: "AuthSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthTokens_UserId",
                table: "AuthTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropTable(
                name: "AuthTokens");

            migrationBuilder.DropTable(
                name: "BotDbApprovedIP");

            migrationBuilder.DropTable(
                name: "BotDbPermission");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
