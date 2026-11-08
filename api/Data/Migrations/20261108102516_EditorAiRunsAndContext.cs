using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EditorAiRunsAndContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "editor_ai_file_texts",
                columns: table => new
                {
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editor_ai_file_texts", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_editor_ai_file_texts_file_assets_FileId",
                        column: x => x.FileId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "editor_ai_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    TargetJson = table.Column<string>(type: "text", nullable: false),
                    ExcludedSourceIdsJson = table.Column<string>(type: "text", nullable: false),
                    AdjustHunkIdsJson = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: false),
                    CoverageJson = table.Column<string>(type: "text", nullable: false),
                    ContextCursorBlockId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Progress = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    AttemptTokens = table.Column<int>(type: "integer", nullable: false),
                    BatchCount = table.Column<int>(type: "integer", nullable: false),
                    AttemptBatchCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editor_ai_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_editor_ai_runs_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "editor_ai_text_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Href = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    Location = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editor_ai_text_chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "editor_ai_run_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editor_ai_run_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_editor_ai_run_events_editor_ai_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "editor_ai_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "editor_ai_run_hunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TargetBlockIdsJson = table.Column<string>(type: "text", nullable: false),
                    BaseFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    TargetJson = table.Column<string>(type: "text", nullable: false),
                    SourceIdsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Signature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editor_ai_run_hunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_editor_ai_run_hunks_editor_ai_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "editor_ai_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_file_texts_State_UpdatedAtUtc",
                table: "editor_ai_file_texts",
                columns: new[] { "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_run_events_RunId_Id",
                table: "editor_ai_run_events",
                columns: new[] { "RunId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_run_hunks_RunId_Order",
                table: "editor_ai_run_hunks",
                columns: new[] { "RunId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_runs_OwnerSub_ClientRequestId",
                table: "editor_ai_runs",
                columns: new[] { "OwnerSub", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_runs_OwnerSub_PageId",
                table: "editor_ai_runs",
                columns: new[] { "OwnerSub", "PageId" },
                unique: true,
                filter: "\"Status\" IN ('queued','gathering','generating')");

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_runs_OwnerSub_PageId_CreatedAtUtc",
                table: "editor_ai_runs",
                columns: new[] { "OwnerSub", "PageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_runs_PageId",
                table: "editor_ai_runs",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_runs_Status_CreatedAtUtc",
                table: "editor_ai_runs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_text_chunks_EntityId_Fingerprint",
                table: "editor_ai_text_chunks",
                columns: new[] { "EntityId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_text_chunks_HoldId",
                table: "editor_ai_text_chunks",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_editor_ai_text_chunks_Kind_SourceId_Ordinal",
                table: "editor_ai_text_chunks",
                columns: new[] { "Kind", "SourceId", "Ordinal" },
                unique: true);
            migrationBuilder.CreateIndex(name: "IX_editor_ai_runs_ExpiresAtUtc", table: "editor_ai_runs", column: "ExpiresAtUtc");
            if (ActiveProvider.Contains("Npgsql"))
                migrationBuilder.Sql("CREATE INDEX \"IX_editor_ai_text_chunks_fts\" ON editor_ai_text_chunks USING GIN (to_tsvector('simple', \"Text\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "editor_ai_file_texts");

            migrationBuilder.DropTable(
                name: "editor_ai_run_events");

            migrationBuilder.DropTable(
                name: "editor_ai_run_hunks");

            migrationBuilder.DropTable(
                name: "editor_ai_text_chunks");

            migrationBuilder.DropTable(
                name: "editor_ai_runs");
        }
    }
}
