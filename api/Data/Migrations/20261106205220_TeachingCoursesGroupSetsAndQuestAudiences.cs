using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeachingCoursesGroupSetsAndQuestAudiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudienceMode",
                table: "learning_quests",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "hold");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupSetId",
                table: "learning_quests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OccurrenceDate",
                table: "learning_quests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Required",
                table: "learning_quests",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "learning_quests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupSetId",
                table: "learning_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "learning_group_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_group_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_group_sets_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quest_audience_groups",
                columns: table => new
                {
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_audience_groups", x => new { x.QuestId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_quest_audience_groups_learning_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "learning_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quest_audience_groups_learning_quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "learning_quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quest_audience_memberships",
                columns: table => new
                {
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_audience_memberships", x => new { x.QuestId, x.MembershipId });
                    table.ForeignKey(
                        name: "FK_quest_audience_memberships_learning_quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "learning_quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quest_audience_memberships_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teaching_courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LearningObjectives = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teaching_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teaching_courses_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teaching_course_resources",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teaching_course_resources", x => new { x.CourseId, x.Kind, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_teaching_course_resources_teaching_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "teaching_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_learning_quests_GroupSetId",
                table: "learning_quests",
                column: "GroupSetId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_quests_SeriesId_OccurrenceDate",
                table: "learning_quests",
                columns: new[] { "SeriesId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_groups_GroupSetId",
                table: "learning_groups",
                column: "GroupSetId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_group_sets_HoldId_Archived",
                table: "learning_group_sets",
                columns: new[] { "HoldId", "Archived" });

            migrationBuilder.CreateIndex(
                name: "IX_quest_audience_groups_GroupId",
                table: "quest_audience_groups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_quest_audience_memberships_MembershipId",
                table: "quest_audience_memberships",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_teaching_course_resources_Kind_ResourceId",
                table: "teaching_course_resources",
                columns: new[] { "Kind", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teaching_courses_HoldId_Archived",
                table: "teaching_courses",
                columns: new[] { "HoldId", "Archived" });

            migrationBuilder.AddForeignKey(
                name: "FK_learning_groups_learning_group_sets_GroupSetId",
                table: "learning_groups",
                column: "GroupSetId",
                principalTable: "learning_group_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_learning_quests_learning_group_sets_GroupSetId",
                table: "learning_quests",
                column: "GroupSetId",
                principalTable: "learning_group_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_learning_groups_learning_group_sets_GroupSetId",
                table: "learning_groups");

            migrationBuilder.DropForeignKey(
                name: "FK_learning_quests_learning_group_sets_GroupSetId",
                table: "learning_quests");

            migrationBuilder.DropTable(
                name: "learning_group_sets");

            migrationBuilder.DropTable(
                name: "quest_audience_groups");

            migrationBuilder.DropTable(
                name: "quest_audience_memberships");

            migrationBuilder.DropTable(
                name: "teaching_course_resources");

            migrationBuilder.DropTable(
                name: "teaching_courses");

            migrationBuilder.DropIndex(
                name: "IX_learning_quests_GroupSetId",
                table: "learning_quests");

            migrationBuilder.DropIndex(
                name: "IX_learning_quests_SeriesId_OccurrenceDate",
                table: "learning_quests");

            migrationBuilder.DropIndex(
                name: "IX_learning_groups_GroupSetId",
                table: "learning_groups");

            migrationBuilder.DropColumn(
                name: "AudienceMode",
                table: "learning_quests");

            migrationBuilder.DropColumn(
                name: "GroupSetId",
                table: "learning_quests");

            migrationBuilder.DropColumn(
                name: "OccurrenceDate",
                table: "learning_quests");

            migrationBuilder.DropColumn(
                name: "Required",
                table: "learning_quests");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "learning_quests");

            migrationBuilder.DropColumn(
                name: "GroupSetId",
                table: "learning_groups");
        }
    }
}
