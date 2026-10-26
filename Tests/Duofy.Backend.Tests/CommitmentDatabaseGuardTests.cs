using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Datenbank schützt die Verträge auch an der API vorbei: Eine Kategorie oder ein Konto unter
/// einem Vertrag lässt sich nicht löschen, eine Person mit allem, was ihr gehört, schon. Dazu die
/// Migration, die Verträge aus der Lernzeit mit passenden Werten füllt (Spec 64, Risiken).
/// </summary>
[Collection(ApiCollection.Name)]
public class CommitmentDatabaseGuardTests(ApiFixture fixture)
{
    private readonly HttpClient client = fixture.Factory.CreateClient();

    /// <summary>Eine Person mit Konto, eigener Kategorie und einem Vertrag auf beiden.</summary>
    private async Task<(string Token, Guid Account, Guid Category)> PersonWithCommitmentAsync()
    {
        var (_, tokens) = await client.SignUpAsync();
        var account = await (await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/accounts", tokens.AccessToken,
            new CreateAccountRequest("Karte", AccountTypes.Checking, 100m, new DateOnly(2026, 10, 1))))).Content.ReadFromJsonAsync<AccountResponse>();
        var category = await (await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/categories", tokens.AccessToken,
            new SaveCategoryRequest("Fitness", "leisure", PotKeys.Wants)))).Content.ReadFromJsonAsync<CategoryResponse>();
        var commitment = await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/commitments", tokens.AccessToken,
            new CreateCommitmentRequest(CommitmentTypes.Contract, "Studio", 29.90m, 1, new DateOnly(2026, 11, 1), null,
                category!.Id, PotKeys.Wants, AccountId: account!.Id, PaymentMethod: PaymentMethods.DirectDebit)));
        Assert.Equal(HttpStatusCode.Created, commitment.StatusCode);
        return (tokens.AccessToken, account.Id, category.Id);
    }

    private Task<string?> SqlErrorAsync(string sql, Guid id) => fixture.Factory.WithDbAsync(async db =>
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, id);
            return (string?)null;
        }
        catch (PostgresException e)
        {
            return e.SqlState;
        }
    });

    [Fact]
    public async Task A_category_under_a_commitment_is_refused_by_the_api_as_in_use()
    {
        var (token, _, category) = await PersonWithCommitmentAsync();

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"/api/v1/categories/{category}", token));

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Equal(ErrorCodes.CategoryInUse, await delete.ReadCodeAsync());
    }

    [Fact]
    public async Task A_category_under_a_commitment_cannot_be_deleted()
    {
        var (_, _, category) = await PersonWithCommitmentAsync();

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlErrorAsync("DELETE FROM categories WHERE id = {0}", category));
    }

    [Fact]
    public async Task An_account_under_a_commitment_cannot_be_deleted()
    {
        var (_, account, _) = await PersonWithCommitmentAsync();

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlErrorAsync("DELETE FROM accounts WHERE id = {0}", account));
    }

    [Fact]
    public async Task Deleting_the_person_takes_accounts_categories_and_commitments_along()
    {
        var (_, account, _) = await PersonWithCommitmentAsync();

        Assert.Null(await SqlErrorAsync("DELETE FROM users WHERE id = (SELECT owner_id FROM accounts WHERE id = {0})", account));
        Assert.Equal(0, await fixture.Factory.WithDbAsync(db => db.Commitments.CountAsync(c => c.AccountId == account)));
    }

    // Risiko der Spec: Verträge, die der Mensch beim Lernen angelegt hat, bleiben erhalten.
    [Fact]
    public async Task The_migration_keeps_older_commitments_and_fills_them_by_their_type()
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
        // Ohne angemeldete Person sähe der Besitzer-Filter keinen Vertrag (Spec 56).
        using var everything = db.Owners.Bypass("Tests: Migration an einer eigenen Datenbank");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("AddBookings");

        var owner = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (id, first_name, last_name, terms_accepted_at, email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count) " +
            "VALUES ({0}, 'Lern', 'Zeit', now(), false, false, false, false, 0)", owner);
        foreach (var type in new[] { "Contract", "Income", "SavingsGoal", "Debt" })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO commitments (id, owner_id, name, amount, type, interval_months, first_due_date) " +
                "VALUES ({0}, {1}, {2}, 10.00, {2}, 1, DATE '2026-01-01')", Guid.NewGuid(), owner, type);
        }

        await migrator.MigrateAsync();

        var rows = await db.Commitments.AsNoTracking().ToDictionaryAsync(c => c.Name);
        Assert.Equal(4, rows.Count);
        Assert.Equal(("needs", "housing.rent"), await PotAndCategoryAsync(db, rows["Contract"].Id));
        Assert.Equal(("income", "income.earned"), await PotAndCategoryAsync(db, rows["Income"].Id));
        Assert.Equal(("savings", "finance.savings"), await PotAndCategoryAsync(db, rows["SavingsGoal"].Id));
        Assert.Equal(("savings", "finance.debt"), await PotAndCategoryAsync(db, rows["Debt"].Id));
        Assert.All(rows.Values, c => Assert.Equal(Duofy.Domain.PaymentMethod.Other, c.PaymentMethod));
        Assert.All(rows.Values, c => Assert.False(c.PassThrough));
    }

    private static async Task<(string Pot, string Key)> PotAndCategoryAsync(DuofyDbContext db, Guid id)
    {
        var row = await db.Database
            .SqlQueryRaw<PotAndKey>("SELECT c.pot AS pot, k.key AS key FROM commitments c JOIN categories k ON k.id = c.category_id WHERE c.id = {0}", id)
            .SingleAsync();
        return (row.Pot, row.Key);
    }

    private sealed record PotAndKey(string Pot, string Key);
}
