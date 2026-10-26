using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quota_needs",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "quota_savings",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "quota_wants",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "plan_month",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "plan_year",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Von Hand: Bestehende Benutzer bekommen oben die 50/30/20-Regel, bestehende Buchungen hier
            // den Monat ihres Datums als Planmonat. Danach fallen die Standardwerte der Spalten weg;
            // jede neue Zeile bekommt ihre Werte von der API.
            migrationBuilder.Sql(
                "UPDATE bookings SET plan_year = EXTRACT(YEAR FROM date)::integer, plan_month = EXTRACT(MONTH FROM date)::integer;");
            migrationBuilder.Sql(
                "ALTER TABLE users ALTER COLUMN quota_needs DROP DEFAULT, ALTER COLUMN quota_wants DROP DEFAULT, ALTER COLUMN quota_savings DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE bookings ALTER COLUMN plan_year DROP DEFAULT, ALTER COLUMN plan_month DROP DEFAULT;");

            migrationBuilder.AddColumn<Guid>(
                name: "position_id",
                table: "booking_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quota_needs = table.Column<int>(type: "integer", nullable: false),
                    quota_wants = table.Column<int>(type: "integer", nullable: false),
                    quota_savings = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    carry_over_income_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    carry_over_expense_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.CheckConstraint("ck_plans_month", "month BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_plans_quotas", "quota_needs + quota_wants + quota_savings = 100");
                    table.ForeignKey(
                        name: "fk_plans_bookings_carry_over_expense_booking_id",
                        column: x => x.carry_over_expense_booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plans_bookings_carry_over_income_booking_id",
                        column: x => x.carry_over_income_booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plans_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commitment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount_planned = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    counter_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pass_through = table.Column<bool>(type: "boolean", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    changed_by_hand = table.Column<bool>(type: "boolean", nullable: false),
                    tick_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_positions", x => x.id);
                    table.CheckConstraint("ck_plan_positions_amount_positive", "amount_planned > 0");
                    table.CheckConstraint("ck_plan_positions_due_day", "due_day BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "fk_plan_positions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_plan_positions_accounts_counter_account_id",
                        column: x => x.counter_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_plan_positions_bookings_tick_booking_id",
                        column: x => x.tick_booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plan_positions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_plan_positions_commitments_commitment_id",
                        column: x => x.commitment_id,
                        principalTable: "commitments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_plan_positions_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_quotas",
                table: "users",
                sql: "quota_needs + quota_wants + quota_savings = 100");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_owner_id_plan_year_plan_month",
                table: "bookings",
                columns: new[] { "owner_id", "plan_year", "plan_month" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_position_id",
                table: "booking_lines",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_account_id",
                table: "plan_positions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_category_id",
                table: "plan_positions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_commitment_id",
                table: "plan_positions",
                column: "commitment_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_counter_account_id",
                table: "plan_positions",
                column: "counter_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_plan_id",
                table: "plan_positions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_positions_tick_booking_id",
                table: "plan_positions",
                column: "tick_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_plans_carry_over_expense_booking_id",
                table: "plans",
                column: "carry_over_expense_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_plans_carry_over_income_booking_id",
                table: "plans",
                column: "carry_over_income_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_plans_owner_id_year_month",
                table: "plans",
                columns: new[] { "owner_id", "year", "month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_booking_lines_plan_positions_position_id",
                table: "booking_lines",
                column: "position_id",
                principalTable: "plan_positions",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_lines_plan_positions_position_id",
                table: "booking_lines");

            migrationBuilder.DropTable(
                name: "plan_positions");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_quotas",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_bookings_owner_id_plan_year_plan_month",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_booking_lines_position_id",
                table: "booking_lines");

            migrationBuilder.DropColumn(
                name: "quota_needs",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quota_savings",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quota_wants",
                table: "users");

            migrationBuilder.DropColumn(
                name: "plan_month",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "plan_year",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "position_id",
                table: "booking_lines");
        }
    }
}
