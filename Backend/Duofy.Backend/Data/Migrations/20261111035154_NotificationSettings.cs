using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reminders_position_id_recipient_id",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "ix_reminders_recipient_id",
                table: "reminders");

            migrationBuilder.RenameColumn(
                name: "time",
                table: "reminder_settings",
                newName: "morning_time");

            migrationBuilder.RenameColumn(
                name: "enabled",
                table: "reminder_settings",
                newName: "overdue");

            migrationBuilder.AlterColumn<Guid>(
                name: "position_id",
                table: "reminders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "covers_from",
                table: "reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "covers_until",
                table: "reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "reminders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "advance_warning",
                table: "reminder_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "due_today",
                table: "reminder_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "evening_enabled",
                table: "reminder_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "evening_time",
                table: "reminder_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "household",
                table: "reminder_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "warning_days",
                table: "reminder_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Spec 118, R11: Die alte Einstellung "Erinnerungen an/aus" steht jetzt in der Spalte "overdue" (umbenannte Spalte
            // "enabled"). Wer sie an hatte, behält seine Uhrzeit (morning_time, umbenannte Spalte "time") und hat alle Schalter
            // an; wer sie aus hatte, hat alle aus. Der Abend und die Vorwarnung folgen demselben Schalter, mit den Vorgaben 19:00 und 2 Tage.
            migrationBuilder.Sql(
                "UPDATE reminder_settings SET due_today = overdue, advance_warning = overdue, household = overdue, " +
                "evening_enabled = overdue, evening_time = TIME '19:00', warning_days = 2;");

            // Bisherige Erinnerungen waren alle "heute fällig".
            migrationBuilder.Sql("UPDATE reminders SET kind = 'due';");

            migrationBuilder.CreateTable(
                name: "household_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_reminders_position_id_recipient_id_day_kind",
                table: "reminders",
                columns: new[] { "position_id", "recipient_id", "day", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_recipient_id_day_kind",
                table: "reminders",
                columns: new[] { "recipient_id", "day", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_household_events_household_id_occurred_at",
                table: "household_events",
                columns: new[] { "household_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Zurück zu einer Erinnerung je Posten und Person: Die Zusammenfassungen fallen weg, und von den Erinnerungen eines Postens
            // an eine Person bleibt nur die jüngste (sonst scheitert der eindeutige Index); "Erinnerungen an" war, wer "heute fällig" an hatte.
            migrationBuilder.Sql("DELETE FROM reminders WHERE position_id IS NULL;");
            migrationBuilder.Sql(
                "DELETE FROM reminders WHERE id IN (" +
                "SELECT id FROM (SELECT id, ROW_NUMBER() OVER (PARTITION BY position_id, recipient_id ORDER BY day DESC, id DESC) AS newest FROM reminders) ranked " +
                "WHERE newest > 1);");
            migrationBuilder.Sql("UPDATE reminder_settings SET overdue = due_today;");

            migrationBuilder.DropTable(
                name: "household_events");

            migrationBuilder.DropTable(
                name: "postpone_counts");

            migrationBuilder.DropIndex(
                name: "ix_reminders_position_id_recipient_id_day_kind",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "ix_reminders_recipient_id_day_kind",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "covers_from",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "covers_until",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "advance_warning",
                table: "reminder_settings");

            migrationBuilder.DropColumn(
                name: "due_today",
                table: "reminder_settings");

            migrationBuilder.DropColumn(
                name: "evening_enabled",
                table: "reminder_settings");

            migrationBuilder.DropColumn(
                name: "evening_time",
                table: "reminder_settings");

            migrationBuilder.DropColumn(
                name: "household",
                table: "reminder_settings");

            migrationBuilder.DropColumn(
                name: "warning_days",
                table: "reminder_settings");

            migrationBuilder.RenameColumn(
                name: "overdue",
                table: "reminder_settings",
                newName: "enabled");

            migrationBuilder.RenameColumn(
                name: "morning_time",
                table: "reminder_settings",
                newName: "time");

            migrationBuilder.AlterColumn<Guid>(
                name: "position_id",
                table: "reminders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_position_id_recipient_id",
                table: "reminders",
                columns: new[] { "position_id", "recipient_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_recipient_id",
                table: "reminders",
                column: "recipient_id");
        }
    }
}
