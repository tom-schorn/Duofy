using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseSnakeCaseNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Commitments",
                table: "Commitments");

            migrationBuilder.RenameTable(
                name: "Commitments",
                newName: "commitments");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "commitments",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "commitments",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "commitments",
                newName: "amount");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "commitments",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "LastDueDate",
                table: "commitments",
                newName: "last_due_date");

            migrationBuilder.RenameColumn(
                name: "IntervalMonths",
                table: "commitments",
                newName: "interval_months");

            migrationBuilder.RenameColumn(
                name: "FirstDueDate",
                table: "commitments",
                newName: "first_due_date");

            migrationBuilder.AddPrimaryKey(
                name: "pk_commitments",
                table: "commitments",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_commitments",
                table: "commitments");

            migrationBuilder.RenameTable(
                name: "commitments",
                newName: "Commitments");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "Commitments",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Commitments",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "amount",
                table: "Commitments",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Commitments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "last_due_date",
                table: "Commitments",
                newName: "LastDueDate");

            migrationBuilder.RenameColumn(
                name: "interval_months",
                table: "Commitments",
                newName: "IntervalMonths");

            migrationBuilder.RenameColumn(
                name: "first_due_date",
                table: "Commitments",
                newName: "FirstDueDate");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Commitments",
                table: "Commitments",
                column: "Id");
        }
    }
}
