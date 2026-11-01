using System.Net;
using System.Net.Http.Json;
using Duofy.Data;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Symbole für Konten (Issue 14): Standard je Art, wählbar, gespeichert, additive Migration.</summary>
[Collection(ApiCollection.Name)]
public class AccountIconTests
{
    private const string Path = "/api/v1/accounts";

    private static readonly DateOnly October1 = new(2026, 10, 1);

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public AccountIconTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private Task<HttpResponseMessage> PostAsync(string token, CreateAccountRequest request) =>
        client.SendAsync(Authorized(HttpMethod.Post, Path, token, request));

    private Task<HttpResponseMessage> PatchAsync(string token, Guid id, UpdateAccountRequest request) =>
        client.SendAsync(Authorized(HttpMethod.Patch, $"{Path}/{id}", token, request));

    private async Task<AccountResponse> CreateAsync(string token, string type, string? icon = null)
    {
        var response = await PostAsync(token, new CreateAccountRequest("Konto", type, 0m, October1, Icon: icon));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private async Task<AccountResponse> UpdateAsync(string token, Guid id, UpdateAccountRequest request)
    {
        var response = await PatchAsync(token, id, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private async Task<List<AccountResponse>> ListAsync(string token)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, Path, token));
        return (await response.Content.ReadFromJsonAsync<List<AccountResponse>>())!;
    }

    // A1 (R1): jede Art hat ein Standardsymbol
    [Theory]
    [InlineData(AccountTypes.Checking, AccountIcons.Bank)]
    [InlineData(AccountTypes.Savings, AccountIcons.Piggy)]
    [InlineData(AccountTypes.Cash, AccountIcons.Cash)]
    public async Task A_new_account_without_a_choice_shows_the_default_icon_of_its_type(string type, string icon)
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var created = await CreateAsync(tokens.AccessToken, type);

        Assert.Equal(icon, created.Icon);
        Assert.Equal(icon, (await ListAsync(tokens.AccessToken)).Single().Icon);
    }

    // A2 (R2)
    [Fact]
    public async Task A_chosen_icon_is_saved_and_listed()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var created = await CreateAsync(tokens.AccessToken, AccountTypes.Checking, AccountIcons.Wallet);

        Assert.Equal(AccountIcons.Wallet, created.Icon);
        Assert.Equal(AccountIcons.Wallet, Assert.Single(await ListAsync(tokens.AccessToken)).Icon);
    }

    // A3 (R2)
    [Fact]
    public async Task Changing_the_icon_keeps_the_other_fields_and_an_empty_icon_goes_back_to_the_default()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var account = await CreateAsync(tokens.AccessToken, AccountTypes.Savings);

        var chosen = await UpdateAsync(tokens.AccessToken, account.Id, new UpdateAccountRequest(Icon: AccountIcons.Coins));
        var renamed = await UpdateAsync(tokens.AccessToken, account.Id, new UpdateAccountRequest(Name: "Neu"));
        var reset = await UpdateAsync(tokens.AccessToken, account.Id, new UpdateAccountRequest(Icon: ""));

        Assert.Equal(AccountIcons.Coins, chosen.Icon);
        Assert.Equal("Konto", chosen.Name);
        Assert.Equal(AccountIcons.Coins, renamed.Icon);
        Assert.Equal(AccountIcons.Piggy, reset.Icon);
    }

    // A4 (R4): ohne Wahl folgt das Symbol der Art, mit Wahl bleibt es
    [Fact]
    public async Task Without_a_choice_the_icon_follows_the_type_and_with_a_choice_it_stays()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var plain = await CreateAsync(tokens.AccessToken, AccountTypes.Checking);
        var picked = await CreateAsync(tokens.AccessToken, AccountTypes.Checking, AccountIcons.Card);

        var plainSavings = await UpdateAsync(tokens.AccessToken, plain.Id, new UpdateAccountRequest(Type: AccountTypes.Savings));
        var pickedSavings = await UpdateAsync(tokens.AccessToken, picked.Id, new UpdateAccountRequest(Type: AccountTypes.Savings));

        Assert.Equal(AccountIcons.Piggy, plainSavings.Icon);
        Assert.Equal(AccountIcons.Card, pickedSavings.Icon);
    }

    // A5 (R2)
    [Theory]
    [InlineData("rocket")]
    [InlineData("Bank")]
    [InlineData(" bank")]
    public async Task An_unknown_icon_answers_400_and_stores_nothing(string icon)
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);

        var create = await PostAsync(tokens.AccessToken, new CreateAccountRequest("Konto", AccountTypes.Checking, 0m, October1, Icon: icon));
        var account = await CreateAsync(tokens.AccessToken, AccountTypes.Checking);
        var update = await PatchAsync(tokens.AccessToken, account.Id, new UpdateAccountRequest(Icon: icon));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(ErrorCodes.AccountIconInvalid, await create.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal(ErrorCodes.AccountIconInvalid, await update.ReadCodeAsync());
        Assert.Equal(AccountIcons.Bank, Assert.Single(await ListAsync(tokens.AccessToken)).Icon);
    }

    [Fact]
    public void The_default_of_an_unknown_type_is_the_bank_and_every_icon_name_is_known()
    {
        Assert.Equal(AccountIcons.Bank, AccountIcons.DefaultFor(null));
        Assert.Equal(AccountIcons.Bank, AccountIcons.DefaultFor("depot"));
        Assert.All(AccountIcons.All, icon => Assert.True(AccountIcons.IsKnown(icon)));
        Assert.Equal(AccountIcons.All.Count, AccountIcons.All.Distinct().Count());
        Assert.Equal(AccountIcons.Wallet, AccountIcons.Effective(AccountTypes.Savings, AccountIcons.Wallet));
        Assert.Equal(AccountIcons.Piggy, AccountIcons.Effective(AccountTypes.Savings, "unbekannt"));
    }

    // A6 (R4): Die Migration ist additiv; bestehende Konten behalten alle Werte und zeigen das Standardsymbol.
    [Fact]
    public async Task The_migration_adds_an_empty_icon_column_and_existing_accounts_show_their_default()
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
        await migrator.MigrateAsync("AddTransferTargetPot");

        var owner = Guid.NewGuid();
        var savings = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash, " +
            "security_stamp, concurrency_stamp, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, " +
            "first_name, last_name, terms_accepted_at, quota_needs, quota_wants, quota_savings, is_instance_admin) " +
            "VALUES ({0}, 'alt@example.org', 'ALT@EXAMPLE.ORG', 'alt@example.org', 'ALT@EXAMPLE.ORG', true, 'x', {1}, {1}, false, false, true, 0, " +
            "'Alt', 'Konto', now(), 50, 30, 20, false)", owner, Guid.NewGuid().ToString("N"));
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO accounts (id, owner_id, name, type, opening_balance, opening_date, active, is_default, counts_as_available, created_at, updated_at) " +
            "VALUES ({0}, {1}, 'Spar', 'Savings', 250.00, DATE '2026-01-01', true, false, false, now(), now())", savings, owner);

        await migrator.MigrateAsync();

        var stored = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == savings);
        Assert.Null(stored.Icon);
        Assert.Equal("Spar", stored.Name);
        Assert.Equal(250.00m, stored.OpeningBalance);
        Assert.Equal(AccountIcons.Piggy, AccountIcons.Effective(AccountTypes.Savings, stored.Icon));
    }
}
