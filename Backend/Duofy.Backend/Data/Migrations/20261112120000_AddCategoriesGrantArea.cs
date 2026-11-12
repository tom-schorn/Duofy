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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
