using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "savings_goal_id",
                table: "commitments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "savings_goal_id",
                table: "booking_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "savings_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    start_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_savings_goals", x => x.id);
                    table.CheckConstraint("ck_savings_goals_start_not_negative", "start_amount >= 0");
                    table.CheckConstraint("ck_savings_goals_target_positive", "target_amount IS NULL OR target_amount > 0");
                    table.ForeignKey(
                        name: "fk_savings_goals_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_savings_goals_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_commitments_savings_goal_id",
                table: "commitments",
                column: "savings_goal_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_savings_goal_id",
                table: "booking_lines",
                column: "savings_goal_id");

            migrationBuilder.CreateIndex(
                name: "ix_savings_goals_account_id",
                table: "savings_goals",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_savings_goals_owner_id",
                table: "savings_goals",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_lines_savings_goals_savings_goal_id",
                table: "booking_lines",
                column: "savings_goal_id",
                principalTable: "savings_goals",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_commitments_savings_goals_savings_goal_id",
                table: "commitments",
                column: "savings_goal_id",
                principalTable: "savings_goals",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_lines_savings_goals_savings_goal_id",
                table: "booking_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_commitments_savings_goals_savings_goal_id",
                table: "commitments");

            migrationBuilder.DropTable(
                name: "savings_goals");

            migrationBuilder.DropIndex(
                name: "ix_commitments_savings_goal_id",
                table: "commitments");

            migrationBuilder.DropIndex(
                name: "ix_booking_lines_savings_goal_id",
                table: "booking_lines");

            migrationBuilder.DropColumn(
                name: "savings_goal_id",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "savings_goal_id",
                table: "booking_lines");
        }
    }
}
