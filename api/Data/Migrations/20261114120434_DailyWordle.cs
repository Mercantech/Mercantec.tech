using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyWordle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wordle_puzzles",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    HostSlug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Theme = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Answer = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wordle_puzzles", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "wordle_schedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wordle_schedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wordle_plays",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    GuessesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wordle_plays", x => new { x.UserSub, x.Date });
                    table.ForeignKey(
                        name: "FK_wordle_plays_wordle_puzzles_Date",
                        column: x => x.Date,
                        principalTable: "wordle_puzzles",
                        principalColumn: "Date",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wordle_plays_Date",
                table: "wordle_plays",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wordle_plays");

            migrationBuilder.DropTable(
                name: "wordle_schedule");

            migrationBuilder.DropTable(
                name: "wordle_puzzles");
        }
    }
}
