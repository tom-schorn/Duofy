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

    [Fact]
    public async Task Ben_reads_annas_categories_with_any_grant_and_only_then()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);

        var without = await client.SendAsync(ben, HttpMethod.Get, $"{Categories}?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.View));
        var annas = await ListAsync(ben, $"?owner={anna.Id}");
        var bens = await ListAsync(ben);

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (without.StatusCode, await without.ReadCodeAsync()));
        Assert.Contains(annas, c => c.Id == kita.Id);
        Assert.Contains(annas, c => c.IsBuiltIn);
        Assert.DoesNotContain(bens, c => c.Id == kita.Id);
    }

    [Fact]
    public async Task Even_with_everything_granted_ben_cannot_change_or_delete_annas_category()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var kita = await OwnCategoryAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete, AccessLevelKeys.Delete));

        var change = await client.SendAsync(ben, HttpMethod.Put, $"{Categories}/{kita.Id}", Kita with { Name = "Hort" });
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Categories}/{kita.Id}");

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal("Kita", await fixture.Factory.WithDbAsync(db => db.Categories.Where(c => c.Id == kita.Id).Select(c => c.Name).SingleAsync()));
    }
}
