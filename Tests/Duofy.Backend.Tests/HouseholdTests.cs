using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Ohne Haushalt bis zum Gründen (Spec 67, R1, R3).</summary>
[Collection(ApiCollection.Name)]
public class HouseholdTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public HouseholdTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    // A1
    [Fact]
    public async Task A_new_registration_has_no_household_until_anna_founds_one()
    {
        var anna = await client.PersonAsync("Anna");

        var before = await client.SendAsync(anna, HttpMethod.Get, Mine);

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.NoHousehold), (before.StatusCode, await before.ReadCodeAsync()));

        var found = await client.SendAsync(anna, HttpMethod.Post, Households, new CreateHouseholdRequest());

        Assert.Equal(HttpStatusCode.Created, found.StatusCode);
        var household = (await found.Content.ReadFromJsonAsync<HouseholdResponse>())!;
        Assert.Equal("Haushalt von Anna", household.Name);
        Assert.Equal((50, 30, 20), (household.QuotaNeeds, household.QuotaWants, household.QuotaSavings));
        var member = Assert.Single(household.Members);
        Assert.Equal(new MemberResponse(anna.Id, "Anna", "Muster", anna.Email, HouseholdRoles.Founder, IsMe: true, GrantLevels.None, GrantLevels.None), member);
        SameHousehold(household, await client.MineAsync(anna));
    }

    // A1
    [Fact]
    public async Task A_second_household_is_refused_and_changes_nothing()
    {
        var anna = await client.FounderAsync("Anna");
        var before = await client.MineAsync(anna);

        var second = await client.SendAsync(anna, HttpMethod.Post, Households, new CreateHouseholdRequest("Noch einer"));

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.AlreadyInHousehold), (second.StatusCode, await second.ReadCodeAsync()));
        SameHousehold(before, await client.MineAsync(anna));
    }

    // A1: Ein Name wird ohne Leerzeichen am Rand übernommen; ein leerer zählt wie keiner.
    [Theory]
    [InlineData(" Familie Muster ", "Familie Muster")]
    [InlineData("   ", "Haushalt von Anna")]
    public async Task A_given_name_is_used(string name, string expected)
    {
        var anna = await client.PersonAsync("Anna");

        var household = await client.FoundAsync(anna, name);

        Assert.Equal(expected, household.Name);
    }

    [Fact]
    public async Task A_name_longer_than_100_characters_is_refused_and_founds_nothing()
    {
        var anna = await client.PersonAsync("Anna");

        var found = await client.SendAsync(anna, HttpMethod.Post, Households, new CreateHouseholdRequest(new string('x', 101)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.HouseholdNameTooLong), (found.StatusCode, await found.ReadCodeAsync()));
        Assert.True(await client.HasNoHouseholdAsync(anna));
    }

    [Fact]
    public async Task Without_sign_in_there_is_no_household()
    {
        var mine = await client.GetAsync(Mine);
        var found = await client.PostAsJsonAsync(Households, new CreateHouseholdRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, found.StatusCode);
    }

    // A2
    [Fact]
    public async Task The_migration_puts_no_existing_user_into_a_household()
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
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("AddMonthlyPlan");

        foreach (var firstName in new[] { "Lena", "Otto" })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (id, first_name, last_name, terms_accepted_at, email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, quota_needs, quota_wants, quota_savings) " +
                "VALUES ({0}, {1}, 'Alt', now(), false, false, false, false, 0, 50, 30, 20)", Guid.NewGuid(), firstName);
        }

        await migrator.MigrateAsync();

        Assert.Equal(2, await db.Users.CountAsync());
        Assert.False(await db.Households.AnyAsync());
        Assert.False(await db.HouseholdMembers.AnyAsync());
    }
}
