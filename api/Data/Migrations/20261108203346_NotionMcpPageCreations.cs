using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotionMcpPageCreations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notion_mcp_page_creations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notion_mcp_page_creations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notion_mcp_page_creations_OwnerSub_ParentPageId_CreatedAtUtc",
                table: "notion_mcp_page_creations",
                columns: new[] { "OwnerSub", "ParentPageId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notion_mcp_page_creations");
        }
    }
}
