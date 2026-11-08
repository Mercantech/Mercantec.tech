using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotionMcpChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notion_mcp_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UndoOf = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UndoneAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notion_mcp_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notion_mcp_changes_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notion_mcp_changes_OwnerSub_PageId_CreatedAtUtc",
                table: "notion_mcp_changes",
                columns: new[] { "OwnerSub", "PageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notion_mcp_changes_PageId",
                table: "notion_mcp_changes",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_notion_mcp_changes_UndoOf",
                table: "notion_mcp_changes",
                column: "UndoOf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notion_mcp_changes");
        }
    }
}
