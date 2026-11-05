using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotionPublishingGroupsAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftFingerprint",
                table: "notion_pages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DraftRevision",
                table: "notion_pages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PublicationVersion",
                table: "notion_pages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PublishedAssetIdsJson",
                table: "notion_pages",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAtUtc",
                table: "notion_pages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedBySub",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PublishedRevision",
                table: "notion_pages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PublishedState",
                table: "notion_pages",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedTitle",
                table: "notion_pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notion_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notion_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notion_assets_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notion_pages_HoldId_PublishedAtUtc",
                table: "notion_pages",
                columns: new[] { "HoldId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notion_assets_Key",
                table: "notion_assets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notion_assets_PageId",
                table: "notion_assets",
                column: "PageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notion_assets");

            migrationBuilder.DropIndex(
                name: "IX_notion_pages_HoldId_PublishedAtUtc",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "DraftFingerprint",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "DraftRevision",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublicationVersion",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedAssetIdsJson",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedBySub",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedRevision",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedState",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedTitle",
                table: "notion_pages");
        }
    }
}
