using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePushReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec 129: The push reminders (specs 84 and 118) moved to Plagevo. The grant area "reminders" goes with them; its rows
            // must be gone before the narrower check constraint comes back, or the constraint would be refused.
            migrationBuilder.Sql("DELETE FROM grants WHERE area = 'reminders';");

            migrationBuilder.DropTable(
                name: "household_events");

            migrationBuilder.DropTable(
                name: "postpone_counts");

            migrationBuilder.DropTable(
                name: "reminder_devices");

            migrationBuilder.DropTable(
                name: "reminder_settings");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_area",
                table: "grants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_grants_area",
                table: "grants",
                sql: "area IN ('plan', 'book', 'accounts', 'commitments', 'savings')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_area",
                table: "grants");

            migrationBuilder.CreateTable(
                name: "household_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_events_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "postpone_counts",
                columns: table => new
                {
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_postpone_counts", x => new { x.position_id, x.due_date });
                    table.ForeignKey(
                        name: "fk_postpone_counts_plan_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "plan_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminder_devices",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    push_token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminder_devices", x => new { x.user_id, x.installation_id });
                    table.ForeignKey(
                        name: "fk_reminder_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminder_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advance_warning = table.Column<bool>(type: "boolean", nullable: false),
                    due_today = table.Column<bool>(type: "boolean", nullable: false),
                    evening_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    evening_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    household = table.Column<bool>(type: "boolean", nullable: false),
                    morning_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    overdue = table.Column<bool>(type: "boolean", nullable: false),
                    warning_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminder_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_reminder_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    covers_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    covers_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminders_plan_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "plan_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reminders_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_grants_area",
                table: "grants",
                sql: "area IN ('plan', 'book', 'accounts', 'commitments', 'savings', 'reminders')");

            migrationBuilder.CreateIndex(
                name: "ix_household_events_household_id_occurred_at",
                table: "household_events",
                columns: new[] { "household_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_position_id_recipient_id_day_kind",
                table: "reminders",
                columns: new[] { "position_id", "recipient_id", "day", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_recipient_id_day_kind",
                table: "reminders",
                columns: new[] { "recipient_id", "day", "kind" });
        }
    }
}
