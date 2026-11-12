using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtInstallmentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue 137: columns only; existing debts and contracts stay unchanged.
            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "debts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "adjusted_open",
                table: "debts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "adjusted_open_on",
                table: "debts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plan_commitment_id",
                table: "debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "debts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "last_amount",
                table: "commitments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "last_amount_by_hand",
                table: "commitments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_debt_plan",
                table: "commitments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_debts_plan_commitment_id",
                table: "debts",
                column: "plan_commitment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_debts_commitments_plan_commitment_id",
                table: "debts",
                column: "plan_commitment_id",
                principalTable: "commitments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_debts_commitments_plan_commitment_id",
                table: "debts");

            migrationBuilder.DropIndex(
                name: "ix_debts_plan_commitment_id",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "adjusted_open",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "adjusted_open_on",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "plan_commitment_id",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "is_debt_plan",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "last_amount_by_hand",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "last_amount",
                table: "commitments");

            // Notes longer than 200 characters are cut when going back, so the narrower column fits.
            migrationBuilder.Sql("UPDATE debts SET note = LEFT(note, 200) WHERE LENGTH(note) > 200;");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "debts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
