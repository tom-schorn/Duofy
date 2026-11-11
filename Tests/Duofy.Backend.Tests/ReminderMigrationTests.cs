using Duofy.Data;
using Duofy.Domain;
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

    // A10 (Spec 118, R11)
    [Fact]
    public async Task The_notification_migration_carries_old_settings_over_and_rolls_back()
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
        await migrator.MigrateAsync("AddReminders");

        var on = Guid.NewGuid();
        var off = Guid.NewGuid();
        foreach (var (id, mail) in new[] { (on, "an@example.org"), (off, "aus@example.org") })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
                "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
                "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
                "VALUES ({0}, {1}, {2}, {1}, {2}, true, 'x', {3}, {3}, false, false, true, 0, 'A', 'B', now(), 50, 30, 20, false)",
                id, mail, mail.ToUpperInvariant(), Guid.NewGuid().ToString("N"));
        }
        await db.Database.ExecuteSqlRawAsync("INSERT INTO reminder_settings (user_id, enabled, time) VALUES ({0}, true, TIME '07:30'), ({1}, false, TIME '08:00')", on, off);

        await migrator.MigrateAsync("NotificationSettings");

        var rows = await db.Database.SqlQueryRaw<MigratedSettings>(
            "SELECT user_id, due_today, overdue, advance_warning, household, morning_time, evening_enabled, evening_time, warning_days FROM reminder_settings").ToListAsync();
        var first = Assert.Single(rows, r => r.UserId == on);
        Assert.Equal((true, true, true, true, new TimeOnly(7, 30), true, new TimeOnly(19, 0), 2),
            (first.DueToday, first.Overdue, first.AdvanceWarning, first.Household, first.MorningTime, first.EveningEnabled, first.EveningTime, first.WarningDays));
        var second = Assert.Single(rows, r => r.UserId == off);
        Assert.Equal((false, false, false, false), (second.DueToday, second.Overdue, second.AdvanceWarning, second.Household));

        // Nach dem Update gibt es mehrere Erinnerungen je Posten und Person: zwei "heute fällig" an verschiedenen Tagen, eine
        // Vorwarnung, eine Zusammenfassung. Zurück bleibt je Posten und Person nur die jüngste, sonst scheitert der eindeutige Index.
        var plan = Guid.NewGuid();
        var rent = Guid.NewGuid();
        var power = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO plans (id, owner_id, quota_needs, quota_wants, quota_savings, created_at, year, month) VALUES ({0}, {1}, 50, 30, 20, now(), 2026, 11)", plan, on);
        foreach (var (position, label) in new[] { (rent, "Miete"), (power, "Strom") })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO plan_positions (id, label, amount_planned, due_day, type, category_id, pot, pass_through, payment_method, changed_by_hand, plan_id) " +
                "VALUES ({0}, {1}, 10, 10, 'contract', {2}, 'needs', false, 'transfer', false, {3})", position, label, CategoryCatalog.IdFor("household.groceries"), plan);
        }
        var newest = Guid.NewGuid();
        var other = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO reminders (id, position_id, recipient_id, day, state, kind) VALUES " +
            "({0}, {2}, {4}, DATE '2026-11-10', 'done', 'due'), ({1}, {2}, {4}, DATE '2026-11-11', 'done', 'due'), " +
            "({5}, {2}, {4}, DATE '2026-11-09', 'done', 'advance'), ({6}, {3}, {4}, DATE '2026-11-10', 'sent', 'due')",
            Guid.NewGuid(), newest, rent, power, on, Guid.NewGuid(), other);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO reminders (id, position_id, recipient_id, day, state, kind, covers_from, covers_until) VALUES ({0}, NULL, {1}, DATE '2026-11-10', 'sent', 'household', now(), now())",
            Guid.NewGuid(), on);

        await migrator.MigrateAsync("AddReminders");

        var kept = await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM reminders").ToListAsync();
        Assert.Equal(new[] { newest, other }.Order(), kept.Order());

        var back = await db.Database.SqlQueryRaw<BackSettings>("SELECT user_id, enabled, time FROM reminder_settings").ToListAsync();
        Assert.Equal((true, new TimeOnly(7, 30)), (back.Single(r => r.UserId == on).Enabled, back.Single(r => r.UserId == on).Time));
        Assert.False(back.Single(r => r.UserId == off).Enabled);
    }

    private sealed record MigratedSettings(
        Guid UserId, bool DueToday, bool Overdue, bool AdvanceWarning, bool Household, TimeOnly MorningTime, bool EveningEnabled, TimeOnly EveningTime, int WarningDays);

    private sealed record BackSettings(Guid UserId, bool Enabled, TimeOnly Time);
}
