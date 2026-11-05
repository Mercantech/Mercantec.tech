using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotionMultipartUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notion_asset_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StorageUploadId = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChunkSizeBytes = table.Column<int>(type: "integer", nullable: false),
                    PartsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AbortRequested = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "bigint", nullable: false),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notion_asset_uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notion_asset_uploads_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notion_asset_uploads_ExpiresAtUtc",
                table: "notion_asset_uploads",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_notion_asset_uploads_Key",
                table: "notion_asset_uploads",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notion_asset_uploads_PageId",
                table: "notion_asset_uploads",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notion_asset_uploads");
        }
    }
}
