using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_area",
                table: "grants");

            migrationBuilder.CreateTable(
                name: "reminder_devices",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    push_token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
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
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
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
                name: "ix_reminders_position_id_recipient_id",
                table: "reminders",
                columns: new[] { "position_id", "recipient_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminders_recipient_id",
                table: "reminders",
                column: "recipient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die alte Prüfregel kennt den Bereich Erinnerungen nicht; ihre Freigaben müssen vorher weg, sonst scheitert das Zurückholen.
            migrationBuilder.Sql("DELETE FROM grants WHERE area = 'reminders';");

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
    }
}
