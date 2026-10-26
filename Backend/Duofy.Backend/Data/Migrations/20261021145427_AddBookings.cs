using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counter_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_bookings_two_different_accounts", "counter_account_id IS NULL OR counter_account_id <> account_id");
                    table.ForeignKey(
                        name: "fk_bookings_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_bookings_accounts_counter_account_id",
                        column: x => x.counter_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_bookings_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    pot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_lines", x => x.id);
                    table.CheckConstraint("ck_booking_lines_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_booking_lines_position", "position BETWEEN 0 AND 19");
                    table.ForeignKey(
                        name: "fk_booking_lines_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_lines_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_booking_id",
                table: "booking_lines",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_lines_category_id",
                table: "booking_lines",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_account_id",
                table: "bookings",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_counter_account_id",
                table: "bookings",
                column: "counter_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_owner_id_date",
                table: "bookings",
                columns: new[] { "owner_id", "date" });

            // Wird eine Person gelöscht, löscht die Datenbank ihre Konten, Kategorien und Buchungen
            // nacheinander mit. Dabei kann eine eigene Kategorie verschwinden, während ihre
            // Buchungszeilen noch da sind. Diese drei Fremdschlüssel prüfen deshalb erst am Ende der
            // Transaktion; ein Konto oder eine Kategorie unter einer Buchung lässt sich trotzdem
            // nie löschen. EF Core kann das nicht beschreiben, deshalb von Hand.
            migrationBuilder.Sql("ALTER TABLE bookings ALTER CONSTRAINT fk_bookings_accounts_account_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE bookings ALTER CONSTRAINT fk_bookings_accounts_counter_account_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE booking_lines ALTER CONSTRAINT fk_booking_lines_categories_category_id DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_lines");

            migrationBuilder.DropTable(
                name: "bookings");
        }
    }
}
