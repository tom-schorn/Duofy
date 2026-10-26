using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grants",
                columns: table => new
                {
                    granter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grantee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grants", x => new { x.granter_id, x.grantee_id, x.area });
                    table.CheckConstraint("ck_grants_area", "area IN ('plan', 'book', 'accounts', 'commitments')");
                    table.CheckConstraint("ck_grants_level", "level IN ('view', 'create', 'edit', 'delete')");
                    table.CheckConstraint("ck_grants_not_self", "granter_id <> grantee_id");
                    table.ForeignKey(
                        name: "fk_grants_users_grantee_id",
                        column: x => x.grantee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_grants_users_granter_id",
                        column: x => x.granter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grants_grantee_id",
                table: "grants",
                column: "grantee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grants");
        }
    }
}
