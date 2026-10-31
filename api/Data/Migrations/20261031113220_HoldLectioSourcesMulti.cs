using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HoldLectioSourcesMulti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hold_lectio_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    LectioHoldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LectioHoldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hold_lectio_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hold_lectio_sources_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hold_lectio_sources_HoldId",
                table: "hold_lectio_sources",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_hold_lectio_sources_LectioHoldKey",
                table: "hold_lectio_sources",
                column: "LectioHoldKey",
                unique: true);

            // Bagudfyldning: eksisterende Hold.LectioHoldKey → hold_lectio_sources
            migrationBuilder.Sql("""
                INSERT INTO hold_lectio_sources ("Id", "HoldId", "LectioHoldKey", "LectioHoldName", "ImportedAtUtc")
                SELECT gen_random_uuid(), h."Id", h."LectioHoldKey", h."Name", h."CreatedAtUtc"
                FROM hold h
                WHERE h."LectioHoldKey" IS NOT NULL
                  AND btrim(h."LectioHoldKey") <> ''
                  AND NOT EXISTS (
                    SELECT 1 FROM hold_lectio_sources s
                    WHERE s."LectioHoldKey" = h."LectioHoldKey"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hold_lectio_sources");
        }
    }
}
