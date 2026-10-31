using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShadowMembershipAndLectioHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_HoldId_UserSub",
                table: "memberships");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSub",
                table: "memberships",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "memberships",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LectioId",
                table: "memberships",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LectioHoldKey",
                table: "hold",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_HoldId_LectioId",
                table: "memberships",
                columns: new[] { "HoldId", "LectioId" },
                unique: true,
                filter: "\"LectioId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_HoldId_UserSub",
                table: "memberships",
                columns: new[] { "HoldId", "UserSub" },
                unique: true,
                filter: "\"UserSub\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_LectioId",
                table: "memberships",
                column: "LectioId");

            migrationBuilder.CreateIndex(
                name: "IX_hold_LectioHoldKey",
                table: "hold",
                column: "LectioHoldKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_HoldId_LectioId",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_memberships_HoldId_UserSub",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_memberships_LectioId",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_hold_LectioHoldKey",
                table: "hold");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "LectioId",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "LectioHoldKey",
                table: "hold");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSub",
                table: "memberships",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_HoldId_UserSub",
                table: "memberships",
                columns: new[] { "HoldId", "UserSub" },
                unique: true);
        }
    }
}
