using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>Freigaben setzen und ansehen (Spec 68, R1, R2, R3).</summary>
[Collection(ApiCollection.Name)]
public class GrantTests
{
    private static readonly UpdateGrantsRequest Everything =
        new(AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete);

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    // Spec 140: Ohne Zeile steht Kategorien auf Löschen; alles andere auf Nichts.
    private static readonly GrantLevels Defaults = GrantLevels.None with { Categories = AccessLevelKeys.Delete };

    private static MemberResponse EntryFor(HouseholdResponse household, Person person) =>
        household.Members.Single(m => m.UserId == person.Id);

    private Task<bool> AnyGrantOfAsync(Person person) =>
        fixture.Factory.WithDbAsync(db => db.Grants.AnyAsync(g => g.GranterId == person.Id || g.GranteeId == person.Id));

    // A1
    [Fact]
    public async Task Anna_sets_bens_levels_and_both_see_them_from_their_side()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var id = (await client.MineAsync(anna)).Id;

        var put = await client.SendAsync(anna, HttpMethod.Put, Grants(id, ben.Id),
            new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.Create));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var expected = new GrantLevels(AccessLevelKeys.View, AccessLevelKeys.Create, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.Delete);
        var answer = (await put.Content.ReadFromJsonAsync<MemberResponse>())!;
        Assert.Equal((ben.Id, expected, Defaults), (answer.UserId, answer.MyGrants, answer.GrantsToMe));
        var annasEntryForBen = EntryFor(await client.MineAsync(anna), ben);
        Assert.Equal((expected, Defaults), (annasEntryForBen.MyGrants, annasEntryForBen.GrantsToMe));
        var bensEntryForAnna = EntryFor(await client.MineAsync(ben), anna);
        Assert.Equal((expected, Defaults), (bensEntryForAnna.GrantsToMe, bensEntryForAnna.MyGrants));
        var bensOwnEntry = EntryFor(await client.MineAsync(ben), ben);
        Assert.Equal((GrantLevels.None, GrantLevels.None), (bensOwnEntry.GrantsToMe, bensOwnEntry.MyGrants));
    }

    // R1, R6: Nur die geschickten Bereiche ändern sich; Nichts löscht die Zeile.
    [Fact]
    public async Task Only_the_areas_sent_change_and_none_deletes_the_row()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, Everything);

        var answer = await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.None));

        Assert.Equal(new GrantLevels(AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.Delete), answer.MyGrants);
        Assert.Equal(3, await fixture.Factory.WithDbAsync(db => db.Grants.CountAsync(g => g.GranterId == anna.Id)));
    }

    // Issue 29: Sparen ist ein eigener Bereich; die Stufe wird gespeichert, ist für beide sichtbar und ändert die anderen nicht.
    [Fact]
    public async Task The_savings_area_is_stored_and_seen_from_both_sides_without_touching_the_others()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var id = (await client.MineAsync(anna)).Id;

        var answer = await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.Edit));

        Assert.Equal(new GrantLevels(AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.None, AccessLevelKeys.Edit, AccessLevelKeys.Delete), answer.MyGrants);
        Assert.Equal(AccessLevelKeys.Edit, EntryFor(await client.MineAsync(ben), anna).GrantsToMe.Savings);
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Grants.CountAsync(g => g.GranterId == anna.Id && g.Area == Domain.Area.Savings)));

        var withdrawn = await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.None));
        Assert.Equal(Defaults, withdrawn.MyGrants);
        Assert.False(await AnyGrantOfAsync(anna));
    }

    // Spec 140, A1: Kategorien ist ein eigener Bereich; die Stufe wird gespeichert, ist für beide sichtbar und ändert die anderen nicht.
    [Fact]
    public async Task The_categories_area_is_stored_and_seen_from_both_sides_without_touching_the_others()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();

        var answer = await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.Create));

        Assert.Equal(GrantLevels.None with { Categories = AccessLevelKeys.Create }, answer.MyGrants);
        Assert.Equal(AccessLevelKeys.Create, EntryFor(await client.MineAsync(ben), anna).GrantsToMe.Categories);
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Grants.CountAsync(g => g.GranterId == anna.Id && g.Area == Domain.Area.Categories)));
        Assert.Contains(GrantAreas.Categories, GrantAreas.All);

        // Ausdrücklich "Nichts" bleibt als Zeile stehen: "nie gesetzt" und "Nichts" sind bei Kategorien zweierlei.
        var withdrawn = await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.None));
        Assert.Equal(GrantLevels.None, withdrawn.MyGrants);
        Assert.Equal(AccessLevel.None, await fixture.Factory.WithDbAsync(db => db.Grants.Where(g => g.GranterId == anna.Id && g.Area == Domain.Area.Categories).Select(g => g.Level).SingleAsync()));
    }

    // R2: Was Ben Anna gibt, ist seine eigene Entscheidung und ändert Annas nicht.
    [Fact]
    public async Task Each_side_sets_only_its_own_grants()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var bens = await client.GrantAsync(ben, anna, new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete));

        Assert.Equal(AccessLevelKeys.View, bens.GrantsToMe.Plan);
        Assert.Equal(AccessLevelKeys.Delete, bens.MyGrants.Plan);
        Assert.Equal(AccessLevelKeys.View, EntryFor(await client.MineAsync(anna), ben).MyGrants.Plan);
    }

    // A2
    [Fact]
    public async Task Anna_cannot_grant_herself_nor_clara_of_another_household()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var id = (await client.MineAsync(anna)).Id;

        var self = await client.SendAsync(anna, HttpMethod.Put, Grants(id, anna.Id), new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var foreign = await client.SendAsync(anna, HttpMethod.Put, Grants(id, clara.Id), new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.CannotGrantSelf), (self.StatusCode, await self.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.MemberNotFound), (foreign.StatusCode, await foreign.ReadCodeAsync()));
        Assert.False(await AnyGrantOfAsync(anna));
    }

    // R2: Wer nicht Mitglied ist, findet den Haushalt nicht; der Geber kommt nur aus dem Token.
    [Fact]
    public async Task Clara_cannot_set_grants_in_annas_household()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var id = (await client.MineAsync(anna)).Id;

        var put = await client.SendAsync(clara, HttpMethod.Put, Grants(id, ben.Id), new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.HouseholdNotFound), (put.StatusCode, await put.ReadCodeAsync()));
        Assert.False(await AnyGrantOfAsync(ben));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("View")]
    [InlineData("")]
    public async Task An_unknown_level_is_refused_and_changes_nothing(string level)
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var id = (await client.MineAsync(anna)).Id;

        var put = await client.SendAsync(anna, HttpMethod.Put, Grants(id, ben.Id), new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: level));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.GrantLevelInvalid), (put.StatusCode, await put.ReadCodeAsync()));
        Assert.False(await AnyGrantOfAsync(anna));
    }

    // A3
    [Fact]
    public async Task Leaving_deletes_every_grant_from_and_to_ben_and_joining_again_starts_at_none()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var id = (await client.MineAsync(anna)).Id;
        await client.GrantAsync(anna, ben, Everything);
        await client.GrantAsync(ben, anna, Everything);

        (await client.SendAsync(ben, HttpMethod.Delete, Members(id, "me"))).EnsureSuccessStatusCode();

        Assert.False(await AnyGrantOfAsync(ben));
        await client.JoinAsync(anna, ben);
        var entry = EntryFor(await client.MineAsync(anna), ben);
        Assert.Equal((Defaults, Defaults), (entry.MyGrants, entry.GrantsToMe));
    }

    // R3: Entfernen löscht ebenso alle Freigaben von und an die Person.
    [Fact]
    public async Task Removing_ben_deletes_every_grant_from_and_to_him()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var id = (await client.MineAsync(anna)).Id;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit));
        await client.GrantAsync(ben, anna, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        (await client.SendAsync(anna, HttpMethod.Delete, Members(id, ben.Id.ToString()))).EnsureSuccessStatusCode();

        Assert.False(await AnyGrantOfAsync(ben));
    }

    // R3: Ein gelöschtes Konto nimmt alle seine Freigaben mit.
    [Fact]
    public async Task Deleting_bens_account_deletes_every_grant_from_and_to_him()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit));
        await client.GrantAsync(ben, anna, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        (await client.SendAsync(ben, HttpMethod.Delete, "/api/v1/users/me", new DeleteMeRequest(ApiDriver.Password))).EnsureSuccessStatusCode();

        Assert.False(await AnyGrantOfAsync(ben));
        Assert.Equal(Defaults, Assert.Single((await client.MineAsync(anna)).Members).MyGrants);
    }
}
