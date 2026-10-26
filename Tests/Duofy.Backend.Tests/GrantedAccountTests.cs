using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Konten einer anderen Person mit Freigabe (Spec 68, R4, R5, R3, R9).</summary>
[Collection(ApiCollection.Name)]
public class GrantedAccountTests
{
    private const string Accounts = "/api/v1/accounts";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantedAccountTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static CreateAccountRequest Giro(Guid? ownerId = null) =>
        new("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1), OwnerId: ownerId);

    private async Task<AccountResponse> AccountOfAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Accounts, Giro());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private Task<Guid> OwnerOfAsync(Guid accountId) =>
        fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == accountId).Select(a => a.OwnerId).SingleAsync());

    // R4: Mit Ansehen sieht Ben Annas Konten, ohne Freigabe sind sie unsichtbar.
    [Fact]
    public async Task Ben_lists_annas_accounts_only_with_view()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountOfAsync(anna);

        var before = await client.SendAsync(ben, HttpMethod.Get, $"{Accounts}?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.View));
        var after = await client.SendAsync(ben, HttpMethod.Get, $"{Accounts}?owner={anna.Id}");
        var own = await client.SendAsync(ben, HttpMethod.Get, Accounts);

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (before.StatusCode, await before.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(giro.Id, Assert.Single((await after.Content.ReadFromJsonAsync<List<AccountResponse>>())!).Id);
        Assert.Empty((await own.Content.ReadFromJsonAsync<List<AccountResponse>>())!);
    }

    // R4, R5: Anlegen braucht Anlegen, Ändern braucht Ändern; das Konto gehört Anna.
    [Fact]
    public async Task Ben_creates_and_changes_annas_accounts_step_by_step()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.View));

        var withView = await client.SendAsync(ben, HttpMethod.Post, Accounts, Giro(anna.Id));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.Create));
        var withCreate = await client.SendAsync(ben, HttpMethod.Post, Accounts, Giro(anna.Id));
        var created = (await withCreate.Content.ReadFromJsonAsync<AccountResponse>())!;
        var renameWithCreate = await client.SendAsync(ben, HttpMethod.Patch, $"{Accounts}/{created.Id}", new UpdateAccountRequest(Name: "Gemeinsam"));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.Edit));
        var renameWithEdit = await client.SendAsync(ben, HttpMethod.Patch, $"{Accounts}/{created.Id}", new UpdateAccountRequest(Name: "Gemeinsam"));

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (withView.StatusCode, await withView.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.Created, withCreate.StatusCode);
        Assert.Equal(anna.Id, await OwnerOfAsync(created.Id));
        Assert.True(created.IsDefault);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (renameWithCreate.StatusCode, await renameWithCreate.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, renameWithEdit.StatusCode);
        Assert.Equal("Gemeinsam", (await renameWithEdit.Content.ReadFromJsonAsync<AccountResponse>())!.Name);
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Accounts.CountAsync(a => a.OwnerId == anna.Id)));
    }

    // R4: Ein neues Standardkonto nimmt Annas bisherigem die Markierung; das ist Ändern, nicht nur Anlegen.
    [Fact]
    public async Task Making_a_new_account_annas_default_needs_edit()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Accounts: AccessLevelKeys.Create));

        var withCreate = await client.SendAsync(ben, HttpMethod.Post, Accounts, Giro(anna.Id) with { IsDefault = true });

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (withCreate.StatusCode, await withCreate.ReadCodeAsync()));
        Assert.True(await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == giro.Id).Select(a => a.IsDefault).SingleAsync()));
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Accounts.CountAsync(a => a.OwnerId == anna.Id)));
    }

    // A8
    [Fact]
    public async Task A_grant_row_between_two_households_grants_nothing()
    {
        var anna = await client.FounderAsync("Anna");
        var ben = await client.FounderAsync("Ben");
        await AccountOfAsync(anna);
        await fixture.Factory.WithDbAsync(async db =>
        {
            db.Grants.Add(new Grant { GranterId = anna.Id, GranteeId = ben.Id, Area = Area.Accounts, Level = AccessLevel.Delete });
            return await db.SaveChangesAsync();
        });

        var list = await client.SendAsync(ben, HttpMethod.Get, $"{Accounts}?owner={anna.Id}");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (list.StatusCode, await list.ReadCodeAsync()));
    }

    // A9: Ein Konto von Clara, die nichts freigegeben hat, sieht aus wie ein unbekanntes.
    [Fact]
    public async Task Claras_account_is_not_found_for_ben_and_stays_as_it_is()
    {
        var (_, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var giro = await AccountOfAsync(clara);

        var patch = await client.SendAsync(ben, HttpMethod.Patch, $"{Accounts}/{giro.Id}", new UpdateAccountRequest(Name: "Meins"));
        var create = await client.SendAsync(ben, HttpMethod.Post, Accounts, Giro(clara.Id));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.AccountNotFound), (patch.StatusCode, await patch.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (create.StatusCode, await create.ReadCodeAsync()));
        Assert.Equal("Giro", await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.Id == giro.Id).Select(a => a.Name).SingleAsync()));
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Accounts.CountAsync(a => a.OwnerId == clara.Id)));
    }
}
