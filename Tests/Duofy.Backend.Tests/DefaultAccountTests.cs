using System.Net;
using System.Net.Http.Json;
using Duofy.Data;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Das erste Konto bei der Registrierung (Spec 13): A1, A2, A4 und die Transaktion.</summary>
[Collection(ApiCollection.Name)]
public class DefaultAccountTests
{
    private const string Path = "/api/v1/accounts";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public DefaultAccountTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private async Task<List<AccountResponse>> ListAsync(string token)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, Path, token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AccountResponse>>())!;
    }

    // A1 und A2: ohne Sprachangabe gilt Deutsch (R4)
    [Fact]
    public async Task A_new_person_has_exactly_one_account_named_Standardkonto()
    {
        var (_, tokens) = await client.SignUpAsync();

        var account = Assert.Single(await ListAsync(tokens.AccessToken));

        Assert.Equal("Standardkonto", account.Name);
        Assert.True(account.Active);
        Assert.Equal(0m, account.OpeningBalance);
        Assert.Equal(0m, account.Balance);
    }

    // Das Konto folgt den Regeln für Konten (Constraints): ein Girokonto wird zum Standardkonto der Person
    [Fact]
    public async Task The_first_account_is_a_checking_account_and_the_default_of_the_person()
    {
        var (_, tokens) = await client.SignUpAsync();

        var account = Assert.Single(await ListAsync(tokens.AccessToken));

        Assert.Equal(AccountTypes.Checking, account.Type);
        Assert.True(account.IsDefault);
        Assert.True(account.CountsAsAvailable);
    }

    // Der Name lässt sich später ändern wie bei jedem Konto (Constraints)
    [Fact]
    public async Task The_name_can_be_changed_later_like_any_account_name()
    {
        var (_, tokens) = await client.SignUpAsync();
        var account = Assert.Single(await ListAsync(tokens.AccessToken));

        var patch = await client.SendAsync(Authorized(
            HttpMethod.Patch, $"{Path}/{account.Id}", tokens.AccessToken, new UpdateAccountRequest(Name: "Giro")));

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("Giro", Assert.Single(await ListAsync(tokens.AccessToken)).Name);
    }

    // Jede Registrierung bekommt ihr eigenes Konto, nie das einer anderen Person (Threats)
    [Fact]
    public async Task Two_new_people_get_one_account_each_and_do_not_see_each_other()
    {
        var (_, anna) = await client.SignUpAsync();
        var (_, berta) = await client.SignUpAsync();

        var annas = Assert.Single(await ListAsync(anna.AccessToken));
        var bertas = Assert.Single(await ListAsync(berta.AccessToken));

        Assert.NotEqual(annas.Id, bertas.Id);
    }

    // Threats / P-041: Das Konto gehört der gerade angelegten Person, auch wenn die Anfrage eine fremde Kennung nennt.
    [Fact]
    public async Task The_first_account_belongs_to_the_new_person_never_to_an_id_in_the_request()
    {
        var (_, anna) = await client.SignUpAsync();
        var annasAccount = Assert.Single(await ListAsync(anna.AccessToken));
        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", anna.AccessToken));
        var annaId = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        var email = NewEmail("berta");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Berta",
            lastName = "Beispiel",
            email,
            password = Password,
            acceptedTerms = true,
            ownerId = annaId,
            language = "en",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var owners = await fixture.Factory.WithDbAsync(async db =>
        {
            var bertaId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            return (Berta: bertaId, Accounts: await db.Accounts.Where(a => a.OwnerId == bertaId).Select(a => a.Name).ToListAsync(),
                AnnasCount: await db.Accounts.CountAsync(a => a.OwnerId == annaId));
        });
        Assert.Equal(["Standardkonto"], owners.Accounts);
        Assert.Equal(1, owners.AnnasCount);
        Assert.Equal(annasAccount.Id, Assert.Single(await ListAsync(anna.AccessToken)).Id);
    }

    // A4: Ein Update legt für bestehende Personen nichts an; auch Anmelden legt nichts an
    [Fact]
    public async Task A_person_with_two_accounts_keeps_exactly_those_two()
    {
        var email = NewEmail();
        (await client.RegisterAsync(Registration(email))).EnsureSuccessStatusCode();
        // So sieht eine Person aus, die vor dem Update registriert wurde: zwei eigene Konten, kein automatisches.
        await fixture.Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            db.Accounts.RemoveRange(db.Accounts.Where(a => a.OwnerId == user.Id));
            await db.SaveChangesAsync();
            var now = DateTimeOffset.UtcNow;
            foreach (var name in new[] { "Giro", "Sparbuch" })
            {
                db.Accounts.Add(new Account
                {
                    Id = Guid.NewGuid(),
                    OwnerId = user.Id,
                    Name = name,
                    Type = name == "Giro" ? AccountType.Checking : AccountType.Savings,
                    OpeningBalance = 10m,
                    OpeningDate = new DateOnly(2026, 10, 1),
                    Active = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync();
            return 0;
        });

        var login = await client.LoginAsync(email);
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        var accounts = await ListAsync(tokens.AccessToken);

        Assert.Equal(["Giro", "Sparbuch"], accounts.Select(a => a.Name).Order().ToList());
    }

    // Risks: Registrierung und Konto in einer Transaktion. Scheitert das Konto, bleibt keine halbe Person zurück.
    // Das Scheitern kommt aus der Datenbank: Ein Auslöser lehnt das Konto genau dieser einen Adresse ab.
    [Fact]
    public async Task A_failing_first_account_leaves_no_half_registered_person()
    {
        var email = NewEmail("fails");
        await RunSqlAsync($"""
            CREATE FUNCTION refuse_test_account() RETURNS trigger AS $$
            BEGIN
                IF EXISTS (SELECT 1 FROM users WHERE id = NEW.owner_id AND email = '{email}') THEN
                    RAISE EXCEPTION 'Test: Konto abgelehnt';
                END IF;
                RETURN NEW;
            END $$ LANGUAGE plpgsql;
            CREATE TRIGGER refuse_test_account BEFORE INSERT ON accounts
                FOR EACH ROW EXECUTE FUNCTION refuse_test_account();
            """);
        try
        {
            var response = await client.RegisterAsync(Registration(email));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.False(await fixture.Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
        }
        finally
        {
            await RunSqlAsync("DROP TRIGGER refuse_test_account ON accounts; DROP FUNCTION refuse_test_account();");
        }
    }

    private Task RunSqlAsync(string sql) =>
        fixture.Factory.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(sql);
            return 0;
        });
}
