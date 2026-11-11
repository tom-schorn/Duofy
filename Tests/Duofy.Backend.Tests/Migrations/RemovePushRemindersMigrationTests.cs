using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Duofy.Api.Tests;

/// <summary>
/// Spec 129, A2: Die Migration RemovePushReminders nimmt die Tabellen der Erinnerungen (Spec 84, 118) und die Freigaben des
/// Bereichs "reminders" mit, an einer Datenbank, die schon alles hat; danach meldet EF keine offenen Änderungen am Modell.
/// </summary>
[Collection(ApiCollection.Name)]
public class RemovePushRemindersMigrationTests(ApiFixture fixture)
{
    private static readonly string[] ReminderTables = ["reminders", "reminder_devices", "reminder_settings", "postpone_counts", "household_events"];

    [Fact]
    public async Task A_database_at_the_old_head_migrates_without_the_reminder_tables_and_grants_and_the_model_has_no_pending_changes()
    {
        await using var db = await CreateDatabaseAsync();
        using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("NotificationSettings");

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
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO reminder_settings (user_id, due_today, overdue, advance_warning, household, morning_time, evening_enabled, evening_time, warning_days) " +
            "VALUES ({0}, true, true, true, true, TIME '09:00', true, TIME '19:00', 2)", granter);
        Assert.Equal(ReminderTables.Order(), await TablesAsync(db, ReminderTables));

        await migrator.MigrateAsync();

        Assert.Empty(await TablesAsync(db, ReminderTables));
        Assert.Equal(["plan"], await db.Database.SqlQueryRaw<string>("SELECT area AS \"Value\" FROM grants").ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Rolling_the_migration_back_brings_the_empty_reminder_tables_and_the_area_check_back()
    {
        await using var db = await CreateDatabaseAsync();
        using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();
        var granter = await InsertUserAsync(db, "geber@example.org");
        var grantee = await InsertUserAsync(db, "nehmer@example.org");
        // At the new head the area is refused by the check.
        await Assert.ThrowsAsync<PostgresException>(() => InsertGrantAsync(db, granter, grantee, "reminders"));

        await migrator.MigrateAsync("NotificationSettings");

        Assert.Equal(ReminderTables.Order(), await TablesAsync(db, ReminderTables));
        // After the rollback the narrower check is gone again and a grant of the area is accepted.
        await InsertGrantAsync(db, granter, grantee, "reminders");
        Assert.Equal(["reminders"], await db.Database.SqlQueryRaw<string>("SELECT area AS \"Value\" FROM grants").ToListAsync());
    }

    private static async Task<Guid> InsertUserAsync(DuofyDbContext db, string mail)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
            "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
            "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
            "VALUES ({0}, {1}, {2}, {1}, {2}, true, 'x', {3}, {3}, false, false, true, 0, 'A', 'B', now(), 50, 30, 20, false)",
            id, mail, mail.ToUpperInvariant(), Guid.NewGuid().ToString("N"));
        return id;
    }

    private static Task<int> InsertGrantAsync(DuofyDbContext db, Guid granter, Guid grantee, string area) =>
        db.Database.ExecuteSqlRawAsync(
            "INSERT INTO grants (granter_id, grantee_id, area, level) VALUES ({0}, {1}, {2}, 'view')", granter, grantee, area);

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

#pragma warning disable EF1003
    // The names are the constants above, never input, so they may stand in the statement as literals.
    private static async Task<List<string>> TablesAsync(DuofyDbContext db, string[] names) =>
        (await db.Database.SqlQueryRaw<string>(
            "SELECT table_name::text AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name::text IN ("
            + string.Join(", ", names.Select(name => $"'{name}'")) + ")")
            .ToListAsync()).Order().ToList();
#pragma warning restore EF1003
}
