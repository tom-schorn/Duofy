using System.Net;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.ApiDriver;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Das Löschen eines Kontos lässt den Haushalt der anderen bestehen (Spec 67, R9).</summary>
[Collection(ApiCollection.Name)]
public class HouseholdDeletionTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public HouseholdDeletionTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private Task<HttpResponseMessage> DeleteAccountAsync(Person person) =>
        client.SendAsync(person, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest(Password));

    // A14
    [Fact]
    public async Task The_earliest_member_becomes_founder_when_the_founder_deletes_the_account()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        var clara = await client.PersonAsync("Clara");
        await client.JoinAsync(anna, ben);
        await client.JoinAsync(anna, clara);
        var id = (await client.MineAsync(anna)).Id;

        var delete = await DeleteAccountAsync(anna);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var household = await client.MineAsync(ben);
        Assert.Equal(id, household.Id);
        Assert.Equal(
            [(ben.Id, HouseholdRoles.Founder), (clara.Id, HouseholdRoles.Member)],
            household.Members.Select(m => (m.UserId, m.Role)));
    }

    // A14
    [Fact]
    public async Task A_household_with_nobody_left_goes_with_the_account()
    {
        var anna = await client.PersonAsync("Anna");
        var id = (await client.FoundAsync(anna)).Id;
        await client.InviteAsync(anna);

        (await DeleteAccountAsync(anna)).EnsureSuccessStatusCode();

        Assert.False(await fixture.Factory.WithDbAsync(db => db.Households.AnyAsync(h => h.Id == id)));
        Assert.False(await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.AnyAsync(i => i.HouseholdId == id)));
    }

    // A14: Ein Mitglied geht einfach; die anderen bleiben, wie sie sind.
    [Fact]
    public async Task A_member_deleting_the_account_just_leaves()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        await client.JoinAsync(anna, ben);

        (await DeleteAccountAsync(ben)).EnsureSuccessStatusCode();

        var household = await client.MineAsync(anna);
        var only = Assert.Single(household.Members);
        Assert.Equal((anna.Id, HouseholdRoles.Founder), (only.UserId, only.Role));
    }

    [Fact]
    public async Task Somebody_in_no_household_deletes_the_account_as_before()
    {
        var ben = await client.PersonAsync("Ben");

        var delete = await DeleteAccountAsync(ben);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
