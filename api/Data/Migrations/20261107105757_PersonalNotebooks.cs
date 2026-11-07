using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonalNotebooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notebook_profiles",
                columns: table => new
                {
                    Sub = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_profiles", x => x.Sub);
                });

            migrationBuilder.CreateTable(
                name: "notebooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notebook_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    YjsState = table.Column<byte[]>(type: "bytea", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    AssetIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notebook_documents_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notebook_documents_notebook_documents_ParentPageId",
                        column: x => x.ParentPageId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notebook_documents_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notebook_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_notebook_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notebook_assets_notebook_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notebook_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notebook_comments_notebook_documents_PageId",
                        column: x => x.PageId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notebook_links",
                columns: table => new
                {
                    FromId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_links", x => new { x.FromId, x.ToId });
                    table.ForeignKey(
                        name: "FK_notebook_links_notebook_documents_FromId",
                        column: x => x.FromId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notebook_links_notebook_documents_ToId",
                        column: x => x.ToId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notebook_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientSub = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notebook_shares_notebook_documents_PageId",
                        column: x => x.PageId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notebook_shares_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notebook_template_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<byte[]>(type: "bytea", nullable: false),
                    AssetIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_template_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notebook_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Cadence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notebook_templates_notebook_documents_Id",
                        column: x => x.Id,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notebook_templates_notebook_template_versions_ActiveVersion~",
                        column: x => x.ActiveVersionId,
                        principalTable: "notebook_template_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notebook_assets_DocumentId",
                table: "notebook_assets",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_assets_Key",
                table: "notebook_assets",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_comments_PageId",
                table: "notebook_comments",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_HoldId",
                table: "notebook_documents",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_NotebookId_ParentPageId",
                table: "notebook_documents",
                columns: new[] { "NotebookId", "ParentPageId" });

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_NotebookId_RequestId",
                table: "notebook_documents",
                columns: new[] { "NotebookId", "RequestId" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_NotebookId_TemplateId_PeriodKey",
                table: "notebook_documents",
                columns: new[] { "NotebookId", "TemplateId", "PeriodKey" },
                unique: true,
                filter: "\"PeriodKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_documents_ParentPageId",
                table: "notebook_documents",
                column: "ParentPageId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_links_ToId",
                table: "notebook_links",
                column: "ToId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_shares_NotebookId_RecipientSub",
                table: "notebook_shares",
                columns: new[] { "NotebookId", "RecipientSub" },
                unique: true,
                filter: "\"PageId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_shares_NotebookId_RecipientSub_PageId",
                table: "notebook_shares",
                columns: new[] { "NotebookId", "RecipientSub", "PageId" },
                unique: true,
                filter: "\"PageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_shares_PageId",
                table: "notebook_shares",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_shares_RecipientSub",
                table: "notebook_shares",
                column: "RecipientSub");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_template_versions_TemplateId_Number",
                table: "notebook_template_versions",
                columns: new[] { "TemplateId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notebook_templates_ActiveVersionId",
                table: "notebook_templates",
                column: "ActiveVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_notebooks_OwnerSub",
                table: "notebooks",
                column: "OwnerSub",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_notebook_template_versions_notebook_templates_TemplateId",
                table: "notebook_template_versions",
                column: "TemplateId",
                principalTable: "notebook_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Match the simple-dictionary expression used by the authorized page search.
                // An expression index keeps the model usable by the SQLite authorization tests.
                migrationBuilder.Sql("""
                    CREATE INDEX "IX_notebook_documents_FullText" ON notebook_documents
                    USING GIN (to_tsvector('simple'::regconfig, "Title" || ' ' || "SearchText"));

                    INSERT INTO notebook_profiles ("Sub", "DisplayName", "IsTeacher", "UpdatedAtUtc")
                    SELECT "UserSub", LEFT("DisplayName", 200), "IsTeacher", CURRENT_TIMESTAMP
                    FROM gift_contacts ON CONFLICT ("Sub") DO NOTHING;

                    INSERT INTO notebook_profiles ("Sub", "DisplayName", "IsTeacher", "UpdatedAtUtc")
                    SELECT "UserSub", LEFT(COALESCE(NULLIF(MAX("DisplayName"), ''), 'LUP-bruger'), 200), false, CURRENT_TIMESTAMP
                    FROM memberships WHERE "UserSub" IS NOT NULL GROUP BY "UserSub"
                    ON CONFLICT ("Sub") DO NOTHING;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notebook_templates_notebook_documents_Id",
                table: "notebook_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_notebook_template_versions_notebook_templates_TemplateId",
                table: "notebook_template_versions");

            migrationBuilder.DropTable(
                name: "notebook_assets");

            migrationBuilder.DropTable(
                name: "notebook_comments");

            migrationBuilder.DropTable(
                name: "notebook_links");

            migrationBuilder.DropTable(
                name: "notebook_profiles");

            migrationBuilder.DropTable(
                name: "notebook_shares");

            migrationBuilder.DropTable(
                name: "notebook_documents");

            migrationBuilder.DropTable(
                name: "notebooks");

            migrationBuilder.DropTable(
                name: "notebook_templates");

            migrationBuilder.DropTable(
                name: "notebook_template_versions");
        }
    }
}
