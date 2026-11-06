using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdempotentGroupRouletteRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_learning_groups_HoldId",
                table: "learning_groups");

            migrationBuilder.AddColumn<string>(
                name: "BatchFingerprint",
                table: "learning_groups",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchOrdinal",
                table: "learning_groups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchRequestId",
                table: "learning_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_groups_HoldId_BatchRequestId_BatchOrdinal",
                table: "learning_groups",
                columns: new[] { "HoldId", "BatchRequestId", "BatchOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_learning_groups_HoldId_BatchRequestId_BatchOrdinal",
                table: "learning_groups");

            migrationBuilder.DropColumn(
                name: "BatchFingerprint",
                table: "learning_groups");

            migrationBuilder.DropColumn(
                name: "BatchOrdinal",
                table: "learning_groups");

            migrationBuilder.DropColumn(
                name: "BatchRequestId",
                table: "learning_groups");

            migrationBuilder.CreateIndex(
                name: "IX_learning_groups_HoldId",
                table: "learning_groups",
                column: "HoldId");
        }
    }
}
