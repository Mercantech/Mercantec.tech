using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UploadedGamesForAvatarRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uploaded_games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntryPoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_uploaded_games_file_assets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_games_FileAssetId",
                table: "uploaded_games",
                column: "FileAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_games_OwnerSub_RequestId",
                table: "uploaded_games",
                columns: new[] { "OwnerSub", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uploaded_games");
        }
    }
}
