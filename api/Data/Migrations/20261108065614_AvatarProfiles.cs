using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvatarProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "avatar_profiles",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedAvatarId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Gold = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    LegacyImportAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatar_profiles", x => x.UserSub);
                    table.CheckConstraint("CK_avatar_profiles_gold", "\"Gold\" >= 0");
                    table.CheckConstraint("CK_avatar_profiles_level", "\"Level\" >= 1");
                    table.CheckConstraint("CK_avatar_profiles_revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "avatar_item_ownerships",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatar_item_ownerships", x => new { x.UserSub, x.ItemId });
                    table.ForeignKey(
                        name: "FK_avatar_item_ownerships_avatar_profiles_UserSub",
                        column: x => x.UserSub,
                        principalTable: "avatar_profiles",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "avatar_ownerships",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    AvatarId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatar_ownerships", x => new { x.UserSub, x.AvatarId });
                    table.ForeignKey(
                        name: "FK_avatar_ownerships_avatar_profiles_UserSub",
                        column: x => x.UserSub,
                        principalTable: "avatar_profiles",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avatar_item_ownerships");

            migrationBuilder.DropTable(
                name: "avatar_ownerships");

            migrationBuilder.DropTable(
                name: "avatar_profiles");
        }
    }
}
