using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmExistingEmailsAndAddPasswordResetLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Konten von vor Spec 53 gelten als bestätigt: Sonst käme auf der gehosteten Version niemand mehr hinein,
            // der sich vor der Bestätigungspflicht registriert hat (A4). Down nimmt das nicht zurück, weil danach
            // niemand mehr weiß, welche Adressen wirklich bestätigt wurden.
            migrationBuilder.Sql("UPDATE users SET email_confirmed = true WHERE NOT email_confirmed;");

            migrationBuilder.CreateTable(
                name: "password_reset_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_links_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_password_reset_links_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_links_created_by_id",
                table: "password_reset_links",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_links_token_hash",
                table: "password_reset_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_links_user_id",
                table: "password_reset_links",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_links");
        }
    }
}
