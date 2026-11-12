using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Duofy.Api.Tests;

/// <summary>
/// Issue 137: Die Migration AddDebtInstallmentPlan läuft von der vorigen Migration bis zum Kopf und zurück, legt ihre Spalten an,
/// und danach meldet EF keine offenen Änderungen am Modell (Designer und Snapshot passen zum Modell).
/// </summary>
[Collection(ApiCollection.Name)]
public class AddDebtInstallmentPlanMigrationTests(ApiFixture fixture)
{
    private static readonly string[] CommitmentColumns = ["is_debt_plan", "last_amount", "last_amount_by_hand"];

    [Fact]
    public async Task The_migration_adds_and_removes_its_columns_and_the_model_has_no_pending_changes()
    {
        await using var db = await CreateDatabaseAsync();
        using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync();

        Assert.Equal(CommitmentColumns, await ColumnsAsync(db, "commitments", CommitmentColumns));
        Assert.Equal(["adjusted_open", "adjusted_open_on", "plan_commitment_id", "reference"],
            await ColumnsAsync(db, "debts", ["adjusted_open", "adjusted_open_on", "plan_commitment_id", "reference"]));
        Assert.False(db.Database.HasPendingModelChanges());

        await migrator.MigrateAsync("PostponePosition");

        Assert.Empty(await ColumnsAsync(db, "commitments", CommitmentColumns));
    }

    private async Task<DuofyDbContext> CreateDatabaseAsync()
    {
        var name = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
        return new DuofyDbContext(new DbContextOptionsBuilder<DuofyDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options, new OwnerContext(NullLogger<OwnerContext>.Instance));
    }

    private static async Task<List<string>> ColumnsAsync(DuofyDbContext db, string table, string[] names) =>
        (await db.Database.SqlQueryRaw<string>(
            "SELECT column_name::text AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = {0} AND column_name::text = ANY({1}) ORDER BY 1",
            table, names).ToListAsync());
}
