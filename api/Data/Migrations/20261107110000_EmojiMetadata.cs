using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mercantec.Tech.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107110000_EmojiMetadata")]
public sealed class EmojiMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "AltText", table: "custom_emojis", type: "character varying(240)", maxLength: 240, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Category", table: "custom_emojis", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "custom");
        migrationBuilder.AddColumn<string>(name: "KeywordsJson", table: "custom_emojis", type: "character varying(4096)", maxLength: 4096, nullable: false, defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AltText", table: "custom_emojis");
        migrationBuilder.DropColumn(name: "Category", table: "custom_emojis");
        migrationBuilder.DropColumn(name: "KeywordsJson", table: "custom_emojis");
    }
}
