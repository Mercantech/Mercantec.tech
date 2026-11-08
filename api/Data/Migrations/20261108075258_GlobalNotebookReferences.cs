using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GlobalNotebookReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "HoldId",
                table: "notion_pages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entity_reference_links",
                columns: table => new
                {
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsMention = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_reference_links", x => new { x.SourceKind, x.SourceId, x.Visibility, x.TargetKind, x.TargetId });
                });

            migrationBuilder.CreateTable(
                name: "notion_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notion_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notion_collections_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reference_index_states",
                columns: table => new
                {
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_index_states", x => new { x.SourceKind, x.SourceId, x.Visibility });
                });

            migrationBuilder.CreateTable(
                name: "reference_mention_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientSub = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_mention_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notion_pages_CollectionId_ParentPageId",
                table: "notion_pages",
                columns: new[] { "CollectionId", "ParentPageId" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_reference_links_TargetKind_TargetId",
                table: "entity_reference_links",
                columns: new[] { "TargetKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_notion_collections_HoldId",
                table: "notion_collections",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_reference_mention_events_RecipientSub_CreatedAtUtc",
                table: "reference_mention_events",
                columns: new[] { "RecipientSub", "CreatedAtUtc" });

            // Keep every existing document and graph URL. A collection's ID is its
            // original root page's ID; descendants inherit it without touching Yjs.
            migrationBuilder.Sql("""
                WITH RECURSIVE trees AS (
                    SELECT p."Id", p."Id" AS root, ARRAY[p."Id"] AS path
                    FROM notion_pages p
                    WHERE p."ParentPageId" IS NULL OR NOT EXISTS (
                        SELECT 1 FROM notion_pages parent WHERE parent."Id" = p."ParentPageId"
                        AND parent."HoldId" IS NOT DISTINCT FROM p."HoldId")
                    UNION ALL
                    SELECT child."Id", trees.root, trees.path || child."Id"
                    FROM notion_pages child JOIN trees ON child."ParentPageId" = trees."Id"
                    JOIN notion_pages parent ON parent."Id" = trees."Id"
                    WHERE child."HoldId" IS NOT DISTINCT FROM parent."HoldId"
                    AND NOT child."Id" = ANY(trees.path)
                )
                UPDATE notion_pages p SET "CollectionId" = trees.root FROM trees WHERE p."Id" = trees."Id";
                -- Preserve even a malformed legacy orphan/cycle as an independent root.
                UPDATE notion_pages SET "CollectionId" = "Id", "ParentPageId" = NULL WHERE "CollectionId" IS NULL;
                INSERT INTO notion_collections ("Id", "HoldId", "CreatedBySub", "CreatedAtUtc")
                SELECT p."Id", p."HoldId", p."CreatedBySub", p."CreatedAtUtc"
                FROM notion_pages p WHERE p."CollectionId" = p."Id";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_notion_pages_notion_collections_CollectionId",
                table: "notion_pages",
                column: "CollectionId",
                principalTable: "notion_collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM notion_pages WHERE "HoldId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while standalone theory notebooks exist';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_notion_pages_notion_collections_CollectionId",
                table: "notion_pages");

            migrationBuilder.DropTable(
                name: "entity_reference_links");

            migrationBuilder.DropTable(
                name: "notion_collections");

            migrationBuilder.DropTable(
                name: "reference_index_states");

            migrationBuilder.DropTable(
                name: "reference_mention_events");

            migrationBuilder.DropIndex(
                name: "IX_notion_pages_CollectionId_ParentPageId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "notion_pages");

            migrationBuilder.AlterColumn<Guid>(
                name: "HoldId",
                table: "notion_pages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
