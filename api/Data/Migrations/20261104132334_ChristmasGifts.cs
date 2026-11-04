using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChristmasGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "christmas_gifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderSub = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientSub = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Wrapping = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_christmas_gifts", x => x.Id);
                    table.CheckConstraint("CK_gifts_different_people", "\"SenderSub\" <> \"RecipientSub\"");
                });

            migrationBuilder.CreateTable(
                name: "gift_contacts",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsTeacher = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_contacts", x => x.UserSub);
                });

            migrationBuilder.CreateIndex(
                name: "IX_christmas_gifts_RecipientSub_CreatedAtUtc",
                table: "christmas_gifts",
                columns: new[] { "RecipientSub", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_christmas_gifts_SenderSub_CreatedAtUtc",
                table: "christmas_gifts",
                columns: new[] { "SenderSub", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "christmas_gifts");

            migrationBuilder.DropTable(
                name: "gift_contacts");
        }
    }
}
