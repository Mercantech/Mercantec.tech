using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations;

public partial class GiftThemesForAvatarRequirements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A historical gift's wrapping does not prove which theme was active.
        migrationBuilder.AddColumn<string>(
            name: "Theme",
            table: "christmas_gifts",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "unknown");

        migrationBuilder.CreateIndex(
            name: "IX_christmas_gifts_SenderSub_Theme_RecipientSub",
            table: "christmas_gifts",
            columns: new[] { "SenderSub", "Theme", "RecipientSub" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_christmas_gifts_SenderSub_Theme_RecipientSub",
            table: "christmas_gifts");

        migrationBuilder.DropColumn(name: "Theme", table: "christmas_gifts");
    }
}
