using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesGrantArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_area",
                table: "grants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_grants_area",
                table: "grants",
                sql: "area IN ('plan', 'book', 'accounts', 'commitments', 'savings', 'categories')");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_level",
                table: "grants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_grants_level",
                table: "grants",
                sql: "level IN ('view', 'create', 'edit', 'delete') OR (level = 'none' AND area = 'categories')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ausdrückliche "none"-Zeilen für Kategorien gibt es vor dieser Migration nicht.
            migrationBuilder.Sql("DELETE FROM grants WHERE area = 'categories'");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grants_level",
                table: "grants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_grants_level",
                table: "grants",
                sql: "level IN ('view', 'create', 'edit', 'delete')");

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
