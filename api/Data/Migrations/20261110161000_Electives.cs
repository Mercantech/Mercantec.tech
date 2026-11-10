using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Electives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "electives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Schedule = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistrationOpensOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistrationClosesOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    ReservedSeats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electives", x => x.Id);
                    table.CheckConstraint("CK_electives_seats", "\"ReservedSeats\" >= 0 AND (\"Capacity\" IS NULL OR (\"Capacity\" > 0 AND \"Capacity\" >= \"ReservedSeats\"))");
                    table.CheckConstraint("CK_electives_status", "\"Status\" IN ('template', 'draft', 'open', 'closed', 'archived')");
                    table.ForeignKey(
                        name: "FK_electives_electives_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "electives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "elective_enrollments",
                columns: table => new
                {
                    ElectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnrolledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elective_enrollments", x => new { x.ElectiveId, x.UserSub });
                    table.ForeignKey(
                        name: "FK_elective_enrollments_electives_ElectiveId",
                        column: x => x.ElectiveId,
                        principalTable: "electives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_elective_enrollments_UserSub",
                table: "elective_enrollments",
                column: "UserSub");

            migrationBuilder.CreateIndex(
                name: "IX_electives_TemplateId",
                table: "electives",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "elective_enrollments");

            migrationBuilder.DropTable(
                name: "electives");
        }
    }
}
