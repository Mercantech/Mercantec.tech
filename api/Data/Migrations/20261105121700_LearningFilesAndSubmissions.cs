using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LearningFilesAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_assets_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "learning_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_groups_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "learning_quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    SubmissionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_quests_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_course_materials_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_course_materials_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_previews",
                columns: table => new
                {
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_previews", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_file_previews_file_assets_FileId",
                        column: x => x.FileId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_group_members",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_group_members", x => new { x.GroupId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_learning_group_members_learning_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "learning_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_learning_group_members_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quest_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DraftText = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quest_submissions_learning_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "learning_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quest_submissions_learning_quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "learning_quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_draft_files",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_draft_files", x => new { x.SubmissionId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_submission_draft_files_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_draft_files_quest_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "quest_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    SubmittedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_revisions_quest_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "quest_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherSub = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_feedback_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_feedback_submission_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "submission_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_participants",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_participants", x => new { x.RevisionId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_submission_participants_submission_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "submission_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_revision_files",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_revision_files", x => new { x.RevisionId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_submission_revision_files_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_revision_files_submission_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "submission_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_feedback_files",
                columns: table => new
                {
                    FeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_feedback_files", x => new { x.FeedbackId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_submission_feedback_files_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_feedback_files_submission_feedback_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "submission_feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_course_materials_FileAssetId",
                table: "course_materials",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_course_materials_HoldId",
                table: "course_materials",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_HoldId",
                table: "file_assets",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_Key",
                table: "file_assets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_OwnerSub",
                table: "file_assets",
                column: "OwnerSub");

            migrationBuilder.CreateIndex(
                name: "IX_learning_group_members_MembershipId",
                table: "learning_group_members",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_groups_HoldId",
                table: "learning_groups",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_quests_HoldId",
                table: "learning_quests",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_quest_submissions_GroupId",
                table: "quest_submissions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_quest_submissions_QuestId_OwnerKey",
                table: "quest_submissions",
                columns: new[] { "QuestId", "OwnerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_draft_files_FileAssetId",
                table: "submission_draft_files",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_feedback_FileAssetId",
                table: "submission_feedback",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_feedback_RevisionId",
                table: "submission_feedback",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_feedback_files_FileAssetId",
                table: "submission_feedback_files",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_participants_UserSub",
                table: "submission_participants",
                column: "UserSub");

            migrationBuilder.CreateIndex(
                name: "IX_submission_revision_files_FileAssetId",
                table: "submission_revision_files",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_submission_revisions_SubmissionId_Number",
                table: "submission_revisions",
                columns: new[] { "SubmissionId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_materials");

            migrationBuilder.DropTable(
                name: "file_previews");

            migrationBuilder.DropTable(
                name: "learning_group_members");

            migrationBuilder.DropTable(
                name: "submission_draft_files");

            migrationBuilder.DropTable(
                name: "submission_feedback_files");

            migrationBuilder.DropTable(
                name: "submission_participants");

            migrationBuilder.DropTable(
                name: "submission_revision_files");

            migrationBuilder.DropTable(
                name: "submission_feedback");

            migrationBuilder.DropTable(
                name: "file_assets");

            migrationBuilder.DropTable(
                name: "submission_revisions");

            migrationBuilder.DropTable(
                name: "quest_submissions");

            migrationBuilder.DropTable(
                name: "learning_groups");

            migrationBuilder.DropTable(
                name: "learning_quests");
        }
    }
}
