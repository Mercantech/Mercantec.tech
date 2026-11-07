using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mercantec.Tech.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107150000_InlineDrawings")]
public sealed class InlineDrawings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("DrawingIdsJson", "notion_pages", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("PublishedDrawingsJson", "notion_pages", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("DrawingIdsJson", "notebook_documents", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("DrawingsJson", "notebook_template_versions", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.Sql("""
            CREATE TABLE drawings (
                "Id" uuid NOT NULL CONSTRAINT "PK_drawings" PRIMARY KEY,
                "NotionPageId" uuid NULL CONSTRAINT "FK_drawings_notion_pages_NotionPageId" REFERENCES notion_pages("Id") ON DELETE RESTRICT,
                "NotebookDocumentId" uuid NULL CONSTRAINT "FK_drawings_notebook_documents_NotebookDocumentId" REFERENCES notebook_documents("Id") ON DELETE RESTRICT,
                "BlockId" varchar(128) NOT NULL, "Mode" varchar(16) NOT NULL,
                "YjsState" bytea NULL, "SceneJson" text NOT NULL, "Fingerprint" varchar(64) NULL,
                "AssetIdsJson" text NOT NULL, "Revision" bigint NOT NULL, "AccessVersion" bigint NOT NULL,
                "PreviewLightAssetId" uuid NULL, "PreviewDarkAssetId" uuid NULL,
                "PreviewFingerprint" varchar(64) NULL, "PreviewRevision" bigint NULL,
                "CreatedBySub" uuid NOT NULL, "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NOT NULL,
                CONSTRAINT "CK_drawings_parent" CHECK (("NotionPageId" IS NOT NULL AND "NotebookDocumentId" IS NULL) OR ("NotionPageId" IS NULL AND "NotebookDocumentId" IS NOT NULL))
            );
            CREATE UNIQUE INDEX "IX_drawings_NotionPageId_BlockId" ON drawings("NotionPageId", "BlockId") WHERE "NotionPageId" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_drawings_NotebookDocumentId_BlockId" ON drawings("NotebookDocumentId", "BlockId") WHERE "NotebookDocumentId" IS NOT NULL;
            CREATE TABLE drawing_grants (
                "Id" uuid NOT NULL CONSTRAINT "PK_drawing_grants" PRIMARY KEY, "DrawingId" uuid NOT NULL CONSTRAINT "FK_drawing_grants_drawings_DrawingId" REFERENCES drawings("Id") ON DELETE CASCADE,
                "UserSub" uuid NULL, "HoldId" uuid NULL CONSTRAINT "FK_drawing_grants_hold_HoldId" REFERENCES "hold"("Id") ON DELETE RESTRICT,
                "DisplayName" varchar(200) NOT NULL, "CreatedAtUtc" timestamptz NOT NULL,
                CONSTRAINT "CK_drawing_grants_principal" CHECK (("UserSub" IS NOT NULL AND "HoldId" IS NULL) OR ("UserSub" IS NULL AND "HoldId" IS NOT NULL))
            );
            CREATE UNIQUE INDEX "IX_drawing_grants_DrawingId_UserSub" ON drawing_grants("DrawingId", "UserSub") WHERE "UserSub" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_drawing_grants_DrawingId_HoldId" ON drawing_grants("DrawingId", "HoldId") WHERE "HoldId" IS NOT NULL;
            CREATE INDEX "IX_drawing_grants_HoldId" ON drawing_grants("HoldId");
            CREATE TABLE drawing_assets (
                "Id" uuid NOT NULL CONSTRAINT "PK_drawing_assets" PRIMARY KEY, "DrawingId" uuid NOT NULL CONSTRAINT "FK_drawing_assets_drawings_DrawingId" REFERENCES drawings("Id") ON DELETE CASCADE,
                "Key" varchar(512) NOT NULL, "OriginalName" varchar(240) NOT NULL, "ContentType" varchar(100) NOT NULL,
                "Kind" varchar(24) NOT NULL, "SizeBytes" bigint NOT NULL, "UploadedBySub" uuid NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
            );
            CREATE INDEX "IX_drawing_assets_DrawingId" ON drawing_assets("DrawingId");
            CREATE TABLE drawing_snapshots (
                "Id" uuid NOT NULL CONSTRAINT "PK_drawing_snapshots" PRIMARY KEY, "DrawingId" uuid NOT NULL CONSTRAINT "FK_drawing_snapshots_drawings_DrawingId" REFERENCES drawings("Id") ON DELETE RESTRICT,
                "Revision" bigint NOT NULL, "Fingerprint" varchar(64) NOT NULL, "YjsState" bytea NOT NULL,
                "SceneJson" text NOT NULL, "AssetIdsJson" text NOT NULL,
                "PreviewLightAssetId" uuid NOT NULL CONSTRAINT "FK_drawing_snapshots_drawing_assets_PreviewLightAssetId" REFERENCES drawing_assets("Id") ON DELETE RESTRICT,
                "PreviewDarkAssetId" uuid NOT NULL CONSTRAINT "FK_drawing_snapshots_drawing_assets_PreviewDarkAssetId" REFERENCES drawing_assets("Id") ON DELETE RESTRICT,
                "CreatedAtUtc" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX "IX_drawing_snapshots_DrawingId_Revision" ON drawing_snapshots("DrawingId", "Revision");
            CREATE INDEX "IX_drawing_snapshots_PreviewLightAssetId" ON drawing_snapshots("PreviewLightAssetId");
            CREATE INDEX "IX_drawing_snapshots_PreviewDarkAssetId" ON drawing_snapshots("PreviewDarkAssetId");
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE drawing_snapshots; DROP TABLE drawing_grants; DROP TABLE drawing_assets; DROP TABLE drawings;");
        migrationBuilder.DropColumn("DrawingIdsJson", "notion_pages"); migrationBuilder.DropColumn("PublishedDrawingsJson", "notion_pages");
        migrationBuilder.DropColumn("DrawingIdsJson", "notebook_documents"); migrationBuilder.DropColumn("DrawingsJson", "notebook_template_versions");
    }
}
