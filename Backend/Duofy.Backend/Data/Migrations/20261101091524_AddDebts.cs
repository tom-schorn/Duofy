using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDebts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "debt_id",
                table: "commitments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "debt_id",
                table: "booking_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "debts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    creditor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debts", x => x.id);
                    table.CheckConstraint("ck_debts_amount_positive", "original_amount > 0");
                    table.CheckConstraint("ck_debts_kind", "kind IN ('claim', 'loan')");
                    table.ForeignKey(
                        name: "fk_debts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_commitments_debt_id",
                table: "commitments",
                column: "debt_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_debt_id",
                table: "booking_lines",
                column: "debt_id");

            migrationBuilder.CreateIndex(
                name: "ix_debts_owner_id",
                table: "debts",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_lines_debts_debt_id",
                table: "booking_lines",
                column: "debt_id",
                principalTable: "debts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_commitments_debts_debt_id",
                table: "commitments",
                column: "debt_id",
                principalTable: "debts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_lines_debts_debt_id",
                table: "booking_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_commitments_debts_debt_id",
                table: "commitments");

            migrationBuilder.DropTable(
                name: "debts");

            migrationBuilder.DropIndex(
                name: "ix_commitments_debt_id",
                table: "commitments");

            migrationBuilder.DropIndex(
                name: "ix_booking_lines_debt_id",
                table: "booking_lines");

            migrationBuilder.DropColumn(
                name: "debt_id",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "debt_id",
                table: "booking_lines");
        }
    }
}
