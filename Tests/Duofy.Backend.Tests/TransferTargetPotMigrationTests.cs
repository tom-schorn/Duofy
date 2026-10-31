using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Migration für den Zieltopf der Umbuchung (Spec 72, R9) ist rein additiv: Eine ältere Umbuchung
/// bleibt wie sie war, nur ihr Zieltopf ist leer.
/// </summary>
[Collection(ApiCollection.Name)]
public class TransferTargetPotMigrationTests(ApiFixture fixture)
{
    [Fact]
    public async Task The_migration_keeps_an_older_transfer_unchanged_and_leaves_its_target_pot_empty()
    {
        // Eine eigene, leere Datenbank im selben Container, zuerst nur bis zur Migration davor.
        var name = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
        await using var db = new DuofyDbContext(new DbContextOptionsBuilder<DuofyDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options, new OwnerContext(NullLogger<OwnerContext>.Instance));
        using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("ConfirmExistingEmailsAndAddPasswordResetLinks");

        var owner = Guid.NewGuid();
        var giro = Guid.NewGuid();
        var savings = Guid.NewGuid();
        var transfer = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
            "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
            "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
            "VALUES ({0}, 'alt@example.org', 'ALT@EXAMPLE.ORG', 'alt@example.org', 'ALT@EXAMPLE.ORG', true, 'x', {1}, {1}, false, false, true, 0, " +
            "'Alt', 'Konto', now(), 50, 30, 20, false)", owner, Guid.NewGuid().ToString("N"));
        foreach (var (id, accountName, type) in new[] { (giro, "Giro", "Checking"), (savings, "Spar", "Savings") })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO accounts (id, owner_id, name, type, opening_balance, opening_date, active, is_default, counts_as_available, created_at, updated_at) " +
                "VALUES ({0}, {1}, {2}, {3}, 1000.00, DATE '2026-01-01', true, false, true, now(), now())", id, owner, accountName, type);
        }
        // Eine Umbuchung aus der Zeit vor dem Zieltopf: eine Zeile im Topf Sparen.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO bookings (id, owner_id, account_id, counter_account_id, date, amount, plan_year, plan_month, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, DATE '2026-10-05', 50.00, 2026, 10, now(), now())", transfer, owner, giro, savings);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO booking_lines (id, booking_id, amount, pot, position) VALUES ({0}, {1}, 50.00, 'savings', 0)", Guid.NewGuid(), transfer);

        await migrator.MigrateAsync();

        var stored = await db.Bookings.AsNoTracking().Include(b => b.Lines).SingleAsync(b => b.Id == transfer);
        Assert.True(stored.IsTransfer);
        Assert.Equal(50.00m, stored.Amount);
        Assert.Equal(Duofy.Domain.Pot.Savings, Assert.Single(stored.Lines).Pot);
        Assert.Null(stored.TargetPot);
    }
}
