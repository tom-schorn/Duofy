using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Duofy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    group = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    suggested_pot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_built_in_or_own", "(owner_id IS NULL AND key IS NOT NULL AND name IS NULL) OR (owner_id IS NOT NULL AND key IS NULL AND name IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_categories_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "group", "key", "name", "owner_id", "suggested_pot" },
                values: new object[,]
                {
                    { new Guid("065d0311-5390-4db2-c66a-edad249c3877"), "transport", "transport.public", null, null, "needs" },
                    { new Guid("0dc233f0-eb66-55ac-d327-95879fddad84"), "personal", "personal.communication", null, null, "needs" },
                    { new Guid("1da16e12-2488-ddb3-64bb-1327a8638591"), "finance", "finance.fees", null, null, "needs" },
                    { new Guid("21015c7a-9594-ade1-3df8-74ed610b1302"), "personal", "personal.donations", null, null, "wants" },
                    { new Guid("22815a53-40c8-7f63-2c17-ef06318760ae"), "personal", "personal.gifts", null, null, "wants" },
                    { new Guid("24b1df91-21fd-2a2a-03c1-c3312d01ee90"), "income", "income.earned", null, null, "income" },
                    { new Guid("299d15ec-237b-de0a-bc15-d06715ff0f0d"), "personal", "personal.taxes", null, null, "needs" },
                    { new Guid("32f714c3-8e3d-7905-293e-9eda918144eb"), "housing", "housing.utilities", null, null, "needs" },
                    { new Guid("35c10a71-6799-5a91-5c7e-8551e82773e3"), "housing", "housing.repairs", null, null, "needs" },
                    { new Guid("3981e1da-351c-651c-12a7-4a5a38fba715"), "children", "children.care", null, null, "needs" },
                    { new Guid("3a80f01c-fa0f-4bd4-61fe-beb3d3e45d26"), "household", "household.cleaning", null, null, "needs" },
                    { new Guid("3e188b16-d2b7-a4bb-6b52-7bdc09af8254"), "finance", "finance.settlement", null, null, "needs" },
                    { new Guid("50ea752a-5d64-47c1-8cc8-2e9c60969cca"), "transport", "transport.fines", null, null, "needs" },
                    { new Guid("516b7bd3-287e-690d-c129-c9223ef00a82"), "leisure", "leisure.subscriptions", null, null, "wants" },
                    { new Guid("55f443a0-c735-5942-d041-a9cb7ba8d2a5"), "finance", "finance.debt", null, null, "savings" },
                    { new Guid("63c8cef6-a087-161e-7d65-bc26a430c1a1"), "children", "children.allowance", null, null, "wants" },
                    { new Guid("661254da-3d39-daa3-3d52-76ac76cfb29b"), "personal", "personal.legal", null, null, "needs" },
                    { new Guid("67d34a3f-b44a-6507-fa30-f56663d05f1c"), "household", "household.pets", null, null, "needs" },
                    { new Guid("6c2f8488-7e4f-7d74-a78e-37f7693f41ec"), "housing", "housing.rent", null, null, "needs" },
                    { new Guid("6c64b224-074b-2d29-bd67-dcad4434cc13"), "finance", "finance.savings", null, null, "savings" },
                    { new Guid("7464d85a-ed8c-846d-2f8c-2adb3a4eaf19"), "household", "household.groceries", null, null, "needs" },
                    { new Guid("7f66c4e6-cb76-14b8-286b-623b965c4fda"), "transport", "transport.insurance", null, null, "needs" },
                    { new Guid("843a2a16-6a7a-2e86-80e9-1afa877a8337"), "household", "household.personal_care", null, null, "needs" },
                    { new Guid("88654aad-e573-4e97-612a-8b9c8a6e4ad2"), "housing", "housing.interior", null, null, "wants" },
                    { new Guid("8b03fe29-bdd1-62f2-cd57-128ae8d8b58e"), "leisure", "leisure.memberships", null, null, "wants" },
                    { new Guid("8de51fc0-5899-a1c6-ae45-f59747f14b9d"), "household", "household.healthcare", null, null, "needs" },
                    { new Guid("922b77e6-86c9-f58b-afa3-1e9e10db264b"), "personal", "personal.education", null, null, "needs" },
                    { new Guid("939832ac-b8e7-52ea-a8f6-90e4d2f7858d"), "transport", "transport.fuel", null, null, "needs" },
                    { new Guid("975a6e32-6f94-269a-0dd7-4366d23c98f4"), "leisure", "leisure.dining", null, null, "wants" },
                    { new Guid("a984064d-712d-16b3-a77b-93978913d069"), "transport", "transport.repairs", null, null, "needs" },
                    { new Guid("be514aac-5f62-a6f6-a2a2-604cbabef161"), "personal", "personal.work", null, null, "needs" },
                    { new Guid("bfac8cb7-bb09-2cae-76f5-c340f0876ba4"), "leisure", "leisure.entertainment", null, null, "wants" },
                    { new Guid("cccd94b0-ca1b-1851-e6f0-81dcd3f3a251"), "personal", "personal.insurance", null, null, "needs" },
                    { new Guid("cd1699af-4b90-da05-f09d-16cae7fd4318"), "finance", "finance.investment", null, null, "wants" },
                    { new Guid("dfabf4f7-57e5-0003-0b2d-d554534b13ef"), "children", "children.school", null, null, "needs" },
                    { new Guid("e7b9dead-d76f-da21-3548-9fed7d145a9c"), "leisure", "leisure.vacation", null, null, "wants" },
                    { new Guid("e9c9a292-f505-a818-a3c1-24b7074e787c"), "household", "household.clothing", null, null, "needs" },
                    { new Guid("ebac8714-dea2-7dd8-0b72-d1e0c93def55"), "income", "income.benefits", null, null, "income" },
                    { new Guid("ebba8208-d77f-a8e2-33b8-3e62791db51a"), "housing", "housing.outdoor", null, null, "wants" },
                    { new Guid("f2f757da-2c28-8712-8c70-44d5b9c70815"), "housing", "housing.insurance", null, null, "needs" },
                    { new Guid("f545fa15-596e-16b7-cf7b-10db24c85ad9"), "leisure", "leisure.hobbies", null, null, "wants" },
                    { new Guid("f6db98a4-1dfe-987d-d718-3119039da809"), "leisure", "leisure.indulgences", null, null, "wants" },
                    { new Guid("f8796253-63e3-322a-3ffb-97418ee8e3a9"), "income", "income.interest", null, null, "income" },
                    { new Guid("f87c2240-4128-6fcd-7639-0b98a22e170d"), "transport", "transport.purchase", null, null, "wants" },
                    { new Guid("fb9c9184-deeb-3c33-7403-34bbef76be39"), "income", "income.other", null, null, "income" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_key",
                table: "categories",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_owner_id",
                table: "categories",
                column: "owner_id");

            // Der Name einer eigenen Kategorie ist je Person eindeutig, ohne Rücksicht auf
            // Groß- und Kleinschreibung. EF Core kann diesen Index nicht beschreiben, deshalb
            // von Hand. Die API prüft den Namen vorher auch gegen die eingebauten Namen;
            // dieser Index fängt nur zwei gleichzeitige Anfragen ab.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_categories_owner_lower_name ON categories (owner_id, lower(name)) WHERE owner_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
