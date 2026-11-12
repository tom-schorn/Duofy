using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Kategorien einer anderen Person: lesen mit irgendeiner Freigabe, schreiben nur selbst (Spec 68, R5).</summary>
[Collection(ApiCollection.Name)]
public class GrantedCategoryTests
{
    private const string Categories = "/api/v1/categories";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantedCategoryTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static readonly SaveCategoryRequest Kita = new("Kita", CategoryGroupKeys.All[0], PotKeys.Needs);

    private async Task<CategoryResponse> OwnCategoryAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Categories, Kita);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    private async Task<List<CategoryResponse>> ListAsync(Person viewer, string query = "")
    {
        var response = await client.SendAsync(viewer, HttpMethod.Get, Categories + query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<CategoryResponse>>())!;
    }

    // Spec 140: Ohne Zeile für Kategorien gilt Löschen für jedes Mitglied des Haushalts; wer draußen ist, findet nichts.
    [Fact]
    public async Task Ben_reads_annas_categories_by_default_and_an_outsider_does_not()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);
        var carl = await client.PersonAsync("Carl");

        var annas = await ListAsync(ben, $"?owner={anna.Id}");
        var bens = await ListAsync(ben);
        var outside = await client.SendAsync(carl, HttpMethod.Get, $"{Categories}?owner={anna.Id}");

        Assert.Contains(annas, c => c.Id == kita.Id);
        Assert.Contains(annas, c => c.IsBuiltIn);
        Assert.DoesNotContain(bens, c => c.Id == kita.Id);
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (outside.StatusCode, await outside.ReadCodeAsync()));
    }

    // Spec 140, A3: Ohne Zeile für den Bereich darf Ben Annas Kategorien anlegen, ändern und löschen.
    [Fact]
    public async Task Without_a_categories_row_ben_may_create_rename_and_delete_annas_categories()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);

        var create = await client.SendAsync(ben, HttpMethod.Post, $"{Categories}?owner={anna.Id}", Kita with { Name = "Hort" });
        var change = await client.SendAsync(ben, HttpMethod.Put, $"{Categories}/{kita.Id}", Kita with { Name = "Krippe" });
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Categories}/{kita.Id}?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Name == "Hort").Select(c => c.OwnerId).SingleAsync()));
    }

    // A1/A2: Mit der Stufe Anlegen legt Ben für Anna an; Ändern und Löschen fehlen, bis Anna sie freigibt.
    [Fact]
    public async Task Create_edit_and_delete_follow_the_categories_level_and_belong_to_the_owner()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.Create));

        var created = await client.SendAsync(ben, HttpMethod.Post, $"{Categories}?owner={anna.Id}", Kita);
        var category = (await created.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var renamed = await client.SendAsync(ben, HttpMethod.Put, $"{Categories}/{category.Id}?owner={anna.Id}", Kita with { Name = "Hort" });
        var deleted = await client.SendAsync(ben, HttpMethod.Delete, $"{Categories}/{category.Id}?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Id == category.Id).Select(c => c.OwnerId).SingleAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (renamed.StatusCode, await renamed.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (deleted.StatusCode, await deleted.ReadCodeAsync()));

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.Delete));
        var renamedNow = await client.SendAsync(ben, HttpMethod.Put, $"{Categories}/{category.Id}?owner={anna.Id}", Kita with { Name = "Hort" });
        var deletedNow = await client.SendAsync(ben, HttpMethod.Delete, $"{Categories}/{category.Id}?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.OK, renamedNow.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletedNow.StatusCode);
    }

    // Auch ohne ?owner= darf Ben fremde Kategorien nicht über die Kennung ändern, wenn nur Anlegen freigegeben ist.
    [Fact]
    public async Task Change_through_the_id_without_owner_is_judged_by_the_owner_of_the_category()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.Edit));

        var change = await client.SendAsync(ben, HttpMethod.Put, $"{Categories}/{kita.Id}", Kita with { Name = "Hort" });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal("Hort", await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Id == kita.Id).Select(c => c.Name).SingleAsync()));
    }

    // Sobald Anna den Bereich setzt, entscheidet er allein: Kategorien Ansehen genügt zum Auflisten, anlegen fehlt.
    [Fact]
    public async Task Once_the_categories_row_exists_it_alone_decides_and_view_alone_lists()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Categories: AccessLevelKeys.View));

        var annas = await ListAsync(ben, $"?owner={anna.Id}");
        var create = await client.SendAsync(ben, HttpMethod.Post, $"{Categories}?owner={anna.Id}", Kita with { Name = "Hort" });

        Assert.Contains(annas, c => c.Id == kita.Id);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    // A5: Außerhalb des Haushalts ist jeder Aufruf mit ?owner= "nicht gefunden", ohne Daten.
    [Fact]
    public async Task An_outsider_gets_not_found_on_every_category_endpoint()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);
        var carl = await client.PersonAsync("Carl");

        var list = await client.SendAsync(carl, HttpMethod.Get, $"{Categories}?owner={anna.Id}");
        var create = await client.SendAsync(carl, HttpMethod.Post, $"{Categories}?owner={anna.Id}", Kita with { Name = "Hort" });
        var change = await client.SendAsync(carl, HttpMethod.Put, $"{Categories}/{kita.Id}?owner={anna.Id}", Kita with { Name = "Hort" });
        var delete = await client.SendAsync(carl, HttpMethod.Delete, $"{Categories}/{kita.Id}?owner={anna.Id}");

        Assert.All(new[] { list, create, change, delete }, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal("Kita", await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Id == kita.Id).Select(c => c.Name).SingleAsync()));
    }

    // Ausdrücklich "Nichts": Die Voreinstellung Löschen gilt nur, wenn der Besitzer den Bereich nie gesetzt hat.
    [Fact]
    public async Task When_the_owner_sets_categories_to_none_the_member_sees_nothing_despite_other_view_grants()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await OwnCategoryAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Categories: AccessLevelKeys.None));

        var list = await client.SendAsync(ben, HttpMethod.Get, $"{Categories}?owner={anna.Id}");
        var create = await client.SendAsync(ben, HttpMethod.Post, $"{Categories}?owner={anna.Id}", Kita with { Name = "Hort" });

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (list.StatusCode, await list.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }
}
