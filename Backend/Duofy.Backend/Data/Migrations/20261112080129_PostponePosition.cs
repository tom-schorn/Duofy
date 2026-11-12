using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostponePosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "moved_from_position_id",
                table: "plan_positions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "moved_to_position_id",
                table: "plan_positions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_moved_from_position_id",
                table: "plan_positions",
                column: "moved_from_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_moved_to_position_id",
                table: "plan_positions",
                column: "moved_to_position_id");

            migrationBuilder.AddForeignKey(
                name: "fk_plan_positions_plan_positions_moved_from_position_id",
                table: "plan_positions",
                column: "moved_from_position_id",
                principalTable: "plan_positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_plan_positions_plan_positions_moved_to_position_id",
                table: "plan_positions",
                column: "moved_to_position_id",
                principalTable: "plan_positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_plan_positions_plan_positions_moved_from_position_id",
                table: "plan_positions");

            migrationBuilder.DropForeignKey(
                name: "fk_plan_positions_plan_positions_moved_to_position_id",
                table: "plan_positions");

            migrationBuilder.DropIndex(
                name: "ix_plan_positions_moved_from_position_id",
                table: "plan_positions");

            migrationBuilder.DropIndex(
                name: "ix_plan_positions_moved_to_position_id",
                table: "plan_positions");

            migrationBuilder.DropColumn(
                name: "moved_from_position_id",
                table: "plan_positions");

            migrationBuilder.DropColumn(
                name: "moved_to_position_id",
                table: "plan_positions");
        }
    }
}
