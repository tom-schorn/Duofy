using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Der Gründer verwaltet, Mitglieder treten aus, Name und Quoten des Haushalts (Spec 67, R7, R8, R2, R3).</summary>
[Collection(ApiCollection.Name)]
public class MembershipTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public MembershipTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    /// <summary>Anna als Gründerin, Ben als Mitglied in ihrem Haushalt.</summary>
    private async Task<(Person Anna, Person Ben, Guid HouseholdId)> AnnaAndBenAsync()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        await client.JoinAsync(anna, ben);
        return (anna, ben, (await client.MineAsync(anna)).Id);
    }

    private static string RoleOf(HouseholdResponse household, Person person) =>
        household.Members.Single(m => m.UserId == person.Id).Role;

    // A10
    [Fact]
    public async Task The_founder_sets_quotas_and_renames_and_ben_sees_it_but_may_not_rename()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();

        var quotas = await client.SendAsync(anna, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(QuotaNeeds: 60, QuotaWants: 20, QuotaSavings: 20));
        var wrong = await client.SendAsync(anna, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(QuotaNeeds: 60, QuotaWants: 30, QuotaSavings: 20));
        var rename = await client.SendAsync(anna, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(Name: " Familie Muster "));
        var bensRename = await client.SendAsync(ben, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(Name: "Bens Haushalt"));

        Assert.Equal(HttpStatusCode.OK, quotas.StatusCode);
        var changed = (await quotas.Content.ReadFromJsonAsync<HouseholdResponse>())!;
        Assert.Equal((60, 20, 20), (changed.QuotaNeeds, changed.QuotaWants, changed.QuotaSavings));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.QuotaInvalid), (wrong.StatusCode, await wrong.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NotHouseholdFounder), (bensRename.StatusCode, await bensRename.ReadCodeAsync()));
        var seenByBen = await client.MineAsync(ben);
        Assert.Equal("Familie Muster", seenByBen.Name);
        Assert.Equal((60, 20, 20), (seenByBen.QuotaNeeds, seenByBen.QuotaWants, seenByBen.QuotaSavings));
    }

    [Theory]
    [InlineData("", null, ErrorCodes.HouseholdNameRequired)]
    [InlineData(null, 60, ErrorCodes.QuotaInvalid)]
    public async Task Invalid_changes_are_refused_and_change_nothing(string? name, int? needsOnly, string code)
    {
        var anna = await client.PersonAsync("Anna");
        var id = (await client.FoundAsync(anna)).Id;

        var patch = await client.SendAsync(anna, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(Name: name, QuotaNeeds: needsOnly));

        Assert.Equal((HttpStatusCode.BadRequest, code), (patch.StatusCode, await patch.ReadCodeAsync()));
        var household = await client.MineAsync(anna);
        Assert.Equal(("Haushalt von Anna", 50), (household.Name, household.QuotaNeeds));
    }

    [Fact]
    public async Task A_name_longer_than_100_characters_is_refused()
    {
        var anna = await client.PersonAsync("Anna");
        var id = (await client.FoundAsync(anna)).Id;

        var patch = await client.SendAsync(anna, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(Name: new string('x', 101)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.HouseholdNameTooLong), (patch.StatusCode, await patch.ReadCodeAsync()));
    }

    // A11
    [Fact]
    public async Task There_is_no_way_to_change_a_role()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();

        var promote = await client.SendAsync(anna, HttpMethod.Patch, Members(id, ben.Id.ToString()), new { role = HouseholdRoles.Founder });
        var takeOver = await client.SendAsync(ben, HttpMethod.Patch, Members(id, ben.Id.ToString()), new { role = HouseholdRoles.Founder });
        var put = await client.SendAsync(anna, HttpMethod.Put, Members(id, ben.Id.ToString()), new { role = HouseholdRoles.Founder });

        foreach (var answer in new[] { promote, takeOver, put })
        {
            Assert.Contains(answer.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }
        var household = await client.MineAsync(anna);
        Assert.Equal((HouseholdRoles.Founder, HouseholdRoles.Member), (RoleOf(household, anna), RoleOf(household, ben)));
    }

    // A12
    [Fact]
    public async Task The_founder_cannot_leave_while_ben_is_in_and_dissolves_the_household_alone()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();
        await client.InviteAsync(anna);

        var annaFirst = await client.SendAsync(anna, HttpMethod.Delete, Members(id, "me"));
        var benLeaves = await client.SendAsync(ben, HttpMethod.Delete, Members(id, "me"));

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.FounderCannotLeave), (annaFirst.StatusCode, await annaFirst.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.NoContent, benLeaves.StatusCode);
        Assert.True(await client.HasNoHouseholdAsync(ben));
        var annas = await client.MineAsync(anna);
        Assert.Equal((anna.Id, HouseholdRoles.Founder), (Assert.Single(annas.Members).UserId, annas.Members[0].Role));

        var annaAlone = await client.SendAsync(anna, HttpMethod.Delete, Members(id, "me"));

        Assert.Equal(HttpStatusCode.NoContent, annaAlone.StatusCode);
        Assert.True(await client.HasNoHouseholdAsync(anna));
        Assert.False(await fixture.Factory.WithDbAsync(db => db.Households.AnyAsync(h => h.Id == id)));
        Assert.False(await fixture.Factory.WithDbAsync(db => db.HouseholdInvitations.AnyAsync(i => i.HouseholdId == id)));
    }

    // A13
    [Fact]
    public async Task Anna_removes_ben_who_keeps_his_data_but_cannot_remove_herself()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();
        var account = await client.SendAsync(ben, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)));
        var accountId = (await account.Content.ReadFromJsonAsync<AccountResponse>())!.Id;

        var remove = await client.SendAsync(anna, HttpMethod.Delete, Members(id, ben.Id.ToString()));
        var self = await client.SendAsync(anna, HttpMethod.Delete, Members(id, anna.Id.ToString()));

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.True(await client.HasNoHouseholdAsync(ben));
        Assert.Equal(ben.Id, await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == accountId).Select(a => a.OwnerId).SingleAsync()));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.CannotRemoveSelf), (self.StatusCode, await self.ReadCodeAsync()));
        Assert.Single((await client.MineAsync(anna)).Members);
    }

    // A13, R7: Nur der Gründer entfernt.
    [Fact]
    public async Task Ben_cannot_remove_anybody()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();
        var clara = await client.PersonAsync("Clara");
        await client.JoinAsync(anna, clara);

        var removeAnna = await client.SendAsync(ben, HttpMethod.Delete, Members(id, anna.Id.ToString()));
        var removeClara = await client.SendAsync(ben, HttpMethod.Delete, Members(id, clara.Id.ToString()));

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NotHouseholdFounder), (removeAnna.StatusCode, await removeAnna.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NotHouseholdFounder), (removeClara.StatusCode, await removeClara.ReadCodeAsync()));
        Assert.Equal(3, (await client.MineAsync(anna)).Members.Count);
    }

    [Fact]
    public async Task Removing_somebody_who_is_not_a_member_is_not_found()
    {
        var anna = await client.PersonAsync("Anna");
        var clara = await client.PersonAsync("Clara");
        var id = (await client.FoundAsync(anna)).Id;

        var remove = await client.SendAsync(anna, HttpMethod.Delete, Members(id, clara.Id.ToString()));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.MemberNotFound), (remove.StatusCode, await remove.ReadCodeAsync()));
    }

    // A15
    [Fact]
    public async Task Clara_from_another_household_finds_nothing_in_annas()
    {
        var (anna, ben, id) = await AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var invitation = await client.InviteAsync(anna);
        var before = await client.MineAsync(anna);

        var answers = new[]
        {
            await client.SendAsync(clara, HttpMethod.Patch, Household(id), new UpdateHouseholdRequest(Name: "Gekapert")),
            await client.SendAsync(clara, HttpMethod.Get, Invitations(id)),
            await client.SendAsync(clara, HttpMethod.Post, Invitations(id)),
            await client.SendAsync(clara, HttpMethod.Delete, $"{Invitations(id)}/{invitation.Id}"),
            await client.SendAsync(clara, HttpMethod.Delete, Members(id, ben.Id.ToString())),
            await client.SendAsync(clara, HttpMethod.Delete, Members(id, "me")),
        };

        foreach (var answer in answers)
        {
            Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.HouseholdNotFound), (answer.StatusCode, await answer.ReadCodeAsync()));
        }
        SameHousehold(before, await client.MineAsync(anna));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Invitation(invitation.Token))).StatusCode);
    }
}
