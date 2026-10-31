using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferTargetPot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_pot",
                table: "bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_pot",
                table: "bookings");
        }
    }
}
