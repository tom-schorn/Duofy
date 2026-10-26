using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitmentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                table: "commitments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "commitments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "counter_account_id",
                table: "commitments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "commitments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<bool>(
                name: "pass_through",
                table: "commitments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "commitments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "pot",
                table: "commitments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "target_amount",
                table: "commitments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "target_date",
                table: "commitments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "commitments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Verträge aus der Lernzeit bleiben erhalten und bekommen Werte, die zu ihrer Art passen:
            // den Topf der Art, eine eingebaute Kategorie dieses Topfs und die Zahlungsart "Sonstiges".
            // Alles lässt sich danach im Dialog ändern. Die Kennungen sind die festen Kennungen aus
            // der Migration AddCategories (Miete, Gehalt & Lohn, Rücklagen, Tilgung).
            migrationBuilder.Sql("""
                UPDATE commitments SET
                    pot = CASE type WHEN 'Income' THEN 'income' WHEN 'SavingsGoal' THEN 'savings' WHEN 'Debt' THEN 'savings' ELSE 'needs' END,
                    category_id = CASE type
                        WHEN 'Income' THEN '24b1df91-21fd-2a2a-03c1-c3312d01ee90'::uuid
                        WHEN 'SavingsGoal' THEN '6c64b224-074b-2d29-bd67-dcad4434cc13'::uuid
                        WHEN 'Debt' THEN '55f443a0-c735-5942-d041-a9cb7ba8d2a5'::uuid
                        ELSE '6c2f8488-7e4f-7d74-a78e-37f7693f41ec'::uuid
                    END,
                    payment_method = 'other',
                    created_at = now(),
                    updated_at = now();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_commitments_account_id",
                table: "commitments",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_commitments_category_id",
                table: "commitments",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_commitments_counter_account_id",
                table: "commitments",
                column: "counter_account_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_commitments_two_different_accounts",
                table: "commitments",
                sql: "counter_account_id IS NULL OR account_id IS NULL OR counter_account_id <> account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_commitments_accounts_account_id",
                table: "commitments",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_commitments_accounts_counter_account_id",
                table: "commitments",
                column: "counter_account_id",
                principalTable: "accounts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_commitments_categories_category_id",
                table: "commitments",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id");

            // Wie bei den Buchungen: Wird eine Person gelöscht, verschwinden ihre Konten, Kategorien
            // und Verträge nacheinander. Diese Fremdschlüssel prüfen deshalb erst am Ende der
            // Transaktion; eine Kategorie oder ein Konto unter einem Vertrag lässt sich trotzdem nie
            // löschen. EF Core kann das nicht beschreiben, deshalb von Hand.
            migrationBuilder.Sql("ALTER TABLE commitments ALTER CONSTRAINT fk_commitments_accounts_account_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE commitments ALTER CONSTRAINT fk_commitments_accounts_counter_account_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE commitments ALTER CONSTRAINT fk_commitments_categories_category_id DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_commitments_accounts_account_id",
                table: "commitments");

            migrationBuilder.DropForeignKey(
                name: "fk_commitments_accounts_counter_account_id",
                table: "commitments");

            migrationBuilder.DropForeignKey(
                name: "fk_commitments_categories_category_id",
                table: "commitments");

            migrationBuilder.DropIndex(
                name: "ix_commitments_account_id",
                table: "commitments");

            migrationBuilder.DropIndex(
                name: "ix_commitments_category_id",
                table: "commitments");

            migrationBuilder.DropIndex(
                name: "ix_commitments_counter_account_id",
                table: "commitments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_commitments_two_different_accounts",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "account_id",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "counter_account_id",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "pass_through",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "pot",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "target_amount",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "target_date",
                table: "commitments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "commitments");
        }
    }
}
