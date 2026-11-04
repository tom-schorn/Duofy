using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Duofy.Api.Tests;

/// <summary>Die Migration der Erinnerungen (Spec 84) lässt sich zurücknehmen, auch wenn schon Freigaben für den neuen Bereich da sind.</summary>
[Collection(ApiCollection.Name)]
public class ReminderMigrationTests(ApiFixture fixture)
{
    [Fact]
    public async Task Rolling_back_deletes_the_reminder_grants_and_keeps_the_others()
    {
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
        await migrator.MigrateAsync();

        var granter = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        foreach (var (id, mail) in new[] { (granter, "geber@example.org"), (grantee, "nehmer@example.org") })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
                "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
                "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
                "VALUES ({0}, {1}, {2}, {1}, {2}, true, 'x', {3}, {3}, false, false, true, 0, 'A', 'B', now(), 50, 30, 20, false)",
                id, mail, mail.ToUpperInvariant(), Guid.NewGuid().ToString("N"));
        }
        foreach (var area in new[] { "plan", "reminders" })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO grants (granter_id, grantee_id, area, level) VALUES ({0}, {1}, {2}, 'view')", granter, grantee, area);
        }

        await migrator.MigrateAsync("AddDebts");

        var areas = await db.Database.SqlQueryRaw<string>("SELECT area AS \"Value\" FROM grants").ToListAsync();
        Assert.Equal(["plan"], areas);
    }
}
