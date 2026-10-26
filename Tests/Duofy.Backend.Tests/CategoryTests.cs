using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>Kategorien: eingebaute lesen, eigene anlegen, ändern, löschen (R3, R4, R11).</summary>
[Collection(ApiCollection.Name)]
public class CategoryTests
{
    private const string Path = "/api/v1/categories";

    private readonly HttpClient client;

    public CategoryTests(ApiFixture fixture)
    {
        client = fixture.Factory.CreateClient();
    }

    private async Task<List<CategoryResponse>> ListAsync(string token)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, Path, token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CategoryResponse>>())!;
    }

    private Task<HttpResponseMessage> SaveAsync(HttpMethod method, string path, string token, string? name, string? group, string? pot) =>
        client.SendAsync(Authorized(method, path, token, new SaveCategoryRequest(name, group, pot)));

    private async Task<CategoryResponse> CreateAsync(string token, string name, string group, string pot)
    {
        var response = await SaveAsync(HttpMethod.Post, Path, token, name, group, pot);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    [Fact]
    public async Task Listing_needs_a_signed_in_person()
    {
        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A4
    [Fact]
    public async Task List_contains_the_45_built_in_categories_grouped_with_suggested_pots()
    {
        var (_, tokens) = await client.SignUpAsync();

        var all = await ListAsync(tokens.AccessToken);

        var builtIn = all.Where(c => c.IsBuiltIn).ToList();
        Assert.Equal(45, builtIn.Count);
        var groceries = Assert.Single(builtIn, c => c.Key == "household.groceries");
        Assert.Equal("Lebensmittel", groceries.Name);
        Assert.Equal("household", groceries.Group);
        Assert.Equal(PotKeys.Needs, groceries.SuggestedPot);
        Assert.Equal(PotKeys.Income, Assert.Single(builtIn, c => c.Key == "income.earned").SuggestedPot);
        // Gruppiert heißt: Die Kategorien einer Gruppe stehen zusammen, die Gruppen in fester Reihenfolge.
        var groupsInOrderOfAppearance = all.Select(c => c.Group).Distinct().ToList();
        var runs = all.Zip(all.Skip(1), (a, b) => a.Group != b.Group).Count(changed => changed) + 1;
        Assert.Equal(groupsInOrderOfAppearance.Count, runs);
        Assert.Equal(CategoryGroupKeys.All.Where(groupsInOrderOfAppearance.Contains), groupsInOrderOfAppearance);
    }

    // A6
    [Fact]
    public async Task Own_category_is_listed_for_its_owner_and_not_for_another_person()
    {
        var (_, anna) = await client.SignUpAsync();
        var (_, ben) = await client.SignUpAsync();

        var category = await CreateAsync(anna.AccessToken, "Friseur Kinder", "children", PotKeys.Needs);

        Assert.False(category.IsBuiltIn);
        Assert.Null(category.Key);
        Assert.Equal("Friseur Kinder", category.Name);
        Assert.Equal("children", category.Group);
        Assert.Equal(PotKeys.Needs, category.SuggestedPot);
        Assert.Contains(await ListAsync(anna.AccessToken), c => c.Id == category.Id);
        Assert.DoesNotContain(await ListAsync(ben.AccessToken), c => c.Id == category.Id);
    }

    // A6
    [Theory]
    [InlineData("FRISEUR KINDER")]
    [InlineData("  friseur kinder ")]
    public async Task Name_must_be_unique_without_case_among_own_categories(string again)
    {
        var (_, tokens) = await client.SignUpAsync();
        await CreateAsync(tokens.AccessToken, "Friseur Kinder", "children", PotKeys.Needs);

        var second = await SaveAsync(HttpMethod.Post, Path, tokens.AccessToken, again, "children", PotKeys.Needs);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.CategoryNameTaken, await second.ReadCodeAsync());
    }

    [Fact]
    public async Task Name_must_not_repeat_a_built_in_name_without_case()
    {
        var (_, tokens) = await client.SignUpAsync();

        var response = await SaveAsync(HttpMethod.Post, Path, tokens.AccessToken, "lebensmittel", "household", PotKeys.Needs);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.CategoryNameTaken, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task Two_people_may_use_the_same_own_name()
    {
        var (_, anna) = await client.SignUpAsync();
        var (_, ben) = await client.SignUpAsync();
        await CreateAsync(anna.AccessToken, "Bastelkram", "leisure", PotKeys.Wants);

        var second = await SaveAsync(HttpMethod.Post, Path, ben.AccessToken, "Bastelkram", "leisure", PotKeys.Wants);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData(null, "children", "needs", ErrorCodes.CategoryNameRequired)]
    [InlineData("   ", "children", "needs", ErrorCodes.CategoryNameRequired)]
    [InlineData("x", "nonsense", "needs", ErrorCodes.CategoryGroupInvalid)]
    [InlineData("x", null, "needs", ErrorCodes.CategoryGroupInvalid)]
    [InlineData("x", "children", "nonsense", ErrorCodes.CategoryPotInvalid)]
    [InlineData("x", "children", null, ErrorCodes.CategoryPotInvalid)]
    // Der Topf Einnahmen passt nur zur Gruppe Einnahmen und umgekehrt.
    [InlineData("x", "children", "income", ErrorCodes.CategoryPotInvalid)]
    [InlineData("x", "income", "needs", ErrorCodes.CategoryPotInvalid)]
    public async Task Invalid_input_answers_400_with_a_code(string? name, string? group, string? pot, string code)
    {
        var (_, tokens) = await client.SignUpAsync();

        var response = await SaveAsync(HttpMethod.Post, Path, tokens.AccessToken, name, group, pot);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ReadCodeAsync());
    }

    [Fact]
    public async Task Name_may_have_60_characters_but_not_61()
    {
        var (_, tokens) = await client.SignUpAsync();

        var ok = await SaveAsync(HttpMethod.Post, Path, tokens.AccessToken, new string('a', 60), "leisure", PotKeys.Wants);
        var tooLong = await SaveAsync(HttpMethod.Post, Path, tokens.AccessToken, new string('b', 61), "leisure", PotKeys.Wants);

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(ErrorCodes.CategoryNameTooLong, await tooLong.ReadCodeAsync());
    }

    [Fact]
    public async Task Own_category_can_be_renamed_and_keeps_its_id()
    {
        var (_, tokens) = await client.SignUpAsync();
        var created = await CreateAsync(tokens.AccessToken, "Alt", "leisure", PotKeys.Wants);

        var renamed = await SaveAsync(HttpMethod.Put, $"{Path}/{created.Id}", tokens.AccessToken, "Neu", "personal", PotKeys.Needs);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var body = (await renamed.Content.ReadFromJsonAsync<CategoryResponse>())!;
        Assert.Equal(created.Id, body.Id);
        Assert.Equal("Neu", body.Name);
        Assert.Equal("personal", body.Group);
        Assert.Equal(PotKeys.Needs, body.SuggestedPot);
    }

    [Fact]
    public async Task Saving_a_category_under_its_own_name_again_is_allowed()
    {
        var (_, tokens) = await client.SignUpAsync();
        var created = await CreateAsync(tokens.AccessToken, "Gleich", "leisure", PotKeys.Wants);

        var again = await SaveAsync(HttpMethod.Put, $"{Path}/{created.Id}", tokens.AccessToken, "GLEICH", "leisure", PotKeys.Needs);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // A7 (der Teil ohne Buchungen; "in Benutzung" lässt sich erst mit den Konten und Buchungen prüfen)
    [Fact]
    public async Task Unused_own_category_can_be_deleted()
    {
        var (_, tokens) = await client.SignUpAsync();
        var created = await CreateAsync(tokens.AccessToken, "Weg", "leisure", PotKeys.Wants);

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"{Path}/{created.Id}", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(await ListAsync(tokens.AccessToken), c => c.Id == created.Id);
    }

    // A17 (Kategorien)
    [Fact]
    public async Task Another_persons_category_answers_404_and_stays_unchanged()
    {
        var (_, anna) = await client.SignUpAsync();
        var (_, ben) = await client.SignUpAsync();
        var created = await CreateAsync(anna.AccessToken, "Annas", "leisure", PotKeys.Wants);

        var put = await SaveAsync(HttpMethod.Put, $"{Path}/{created.Id}", ben.AccessToken, "Bens", "leisure", PotKeys.Wants);
        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"{Path}/{created.Id}", ben.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Contains(await ListAsync(anna.AccessToken), c => c.Id == created.Id && c.Name == "Annas");
    }

    [Fact]
    public async Task Built_in_categories_cannot_be_changed_or_deleted()
    {
        var (_, tokens) = await client.SignUpAsync();
        var groceries = (await ListAsync(tokens.AccessToken)).Single(c => c.Key == "household.groceries");

        var put = await SaveAsync(HttpMethod.Put, $"{Path}/{groceries.Id}", tokens.AccessToken, "Mein", "household", PotKeys.Needs);
        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, $"{Path}/{groceries.Id}", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_person_deletes_their_own_categories_but_not_the_built_in_ones()
    {
        var (_, tokens) = await client.SignUpAsync();
        await CreateAsync(tokens.AccessToken, "Vergänglich", "leisure", PotKeys.Wants);

        var delete = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/v1/users/me", tokens.AccessToken, new DeleteMeRequest(Password)));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var (_, other) = await client.SignUpAsync();
        var remaining = await ListAsync(other.AccessToken);
        Assert.Equal(45, remaining.Count(c => c.IsBuiltIn));
        Assert.DoesNotContain(remaining, c => c.Name == "Vergänglich");
    }
}
