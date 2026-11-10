using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookTreeAndOverview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "notion_pages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditedByName",
                table: "notion_pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LifecycleVersion",
                table: "notion_pages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedEditedAtUtc",
                table: "notion_pages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedEditorName",
                table: "notion_pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "notion_pages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrashRootId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IconEmojiId",
                table: "notion_collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedTitle",
                table: "notion_collections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "notion_collections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IconEmojiId",
                table: "notebooks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "notebook_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditedByName",
                table: "notebook_documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LifecycleVersion",
                table: "notebook_documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "notebook_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrashRootId",
                table: "notebook_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notion_pages_TrashRootId",
                table: "notion_pages",
                column: "TrashRootId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_TrashRootId",
                table: "notebook_documents",
                column: "TrashRootId");

            migrationBuilder.Sql("""
                UPDATE notion_collections AS collection
                SET "Title" = root."Title",
                    "PublishedTitle" = CASE WHEN root."PublishedAtUtc" IS NOT NULL AND root."PublishedState" IS NOT NULL THEN root."PublishedTitle" END
                FROM notion_pages AS root
                WHERE root."Id" = collection."Id";

                -- Earlier course-page creation could omit the collection on children.
                WITH RECURSIVE inherited AS (
                    SELECT child."Id", parent."CollectionId", child."HoldId"
                    FROM notion_pages AS child
                    JOIN notion_pages AS parent ON parent."Id" = child."ParentPageId"
                        AND parent."HoldId" IS NOT DISTINCT FROM child."HoldId"
                    WHERE child."CollectionId" IS NULL AND parent."CollectionId" IS NOT NULL
                    UNION ALL
                    SELECT child."Id", inherited."CollectionId", child."HoldId"
                    FROM notion_pages AS child
                    JOIN inherited ON inherited."Id" = child."ParentPageId"
                        AND inherited."HoldId" IS NOT DISTINCT FROM child."HoldId"
                    WHERE child."CollectionId" IS NULL
                )
                UPDATE notion_pages AS page SET "CollectionId" = inherited."CollectionId"
                FROM inherited WHERE inherited."Id" = page."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notion_pages_TrashRootId",
                table: "notion_pages");

            migrationBuilder.DropIndex(
                name: "IX_notebook_documents_TrashRootId",
                table: "notebook_documents");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "LastEditedByName",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "LifecycleVersion",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedEditedAtUtc",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedEditorName",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "TrashRootId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "IconEmojiId",
                table: "notion_collections");

            migrationBuilder.DropColumn(
                name: "PublishedTitle",
                table: "notion_collections");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "notion_collections");

            migrationBuilder.DropColumn(
                name: "IconEmojiId",
                table: "notebooks");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "notebook_documents");

            migrationBuilder.DropColumn(
                name: "LastEditedByName",
                table: "notebook_documents");

            migrationBuilder.DropColumn(
                name: "LifecycleVersion",
                table: "notebook_documents");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "notebook_documents");

            migrationBuilder.DropColumn(
                name: "TrashRootId",
                table: "notebook_documents");
        }
    }
}
