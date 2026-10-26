using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Verträge einer anderen Person mit Freigabe (Spec 68, R4, R5, R9).</summary>
[Collection(ApiCollection.Name)]
public class GrantedCommitmentTests
{
    private const string Commitments = "/api/v1/commitments";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantedCommitmentTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static CreateCommitmentRequest Rent(Guid? ownerId = null) => new(
        CommitmentTypes.Contract, "Miete", 890.00m, 1, new DateOnly(2026, 11, 1), null,
        CategoryCatalog.IdFor("housing.rent"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder, OwnerId: ownerId);

    private async Task<CommitmentResponse> RentOfAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Commitments, Rent());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
    }

    // A4
    [Fact]
    public async Task Ben_sees_annas_contracts_with_view_creates_them_only_with_create_and_cannot_change_them()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var rent = await RentOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.View));

        var list = await client.SendAsync(ben, HttpMethod.Get, $"{Commitments}?owner={anna.Id}");
        var withView = await client.SendAsync(ben, HttpMethod.Post, Commitments, Rent(anna.Id));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Create));
        var withCreate = await client.SendAsync(ben, HttpMethod.Post, Commitments, Rent(anna.Id));
        var created = (await withCreate.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{created.Id}", new UpdateCommitmentRequest(Name: "Kaltmiete"));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(rent.Id, Assert.Single((await list.Content.ReadFromJsonAsync<List<CommitmentResponse>>())!).Id);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (withView.StatusCode, await withView.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.Created, withCreate.StatusCode);
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Commitments.Where(c => c.Id == created.Id).Select(c => c.OwnerId).SingleAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (change.StatusCode, await change.ReadCodeAsync()));
        Assert.Equal("Miete", await fixture.Factory.WithDbAsync(db => db.Commitments.Where(c => c.Id == created.Id).Select(c => c.Name).SingleAsync()));
    }

    // R4: Ändern mit Ändern, Beenden erst mit Löschen.
    [Fact]
    public async Task Ben_changes_annas_contract_with_edit_and_ends_it_only_with_delete()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var rent = await RentOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Edit));

        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rent.Id}", new UpdateCommitmentRequest(Name: "Kaltmiete"));
        var endWithEdit = await client.SendAsync(ben, HttpMethod.Post, $"{Commitments}/{rent.Id}/end");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Delete));
        var endWithDelete = await client.SendAsync(ben, HttpMethod.Post, $"{Commitments}/{rent.Id}/end");

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal("Kaltmiete", (await change.Content.ReadFromJsonAsync<CommitmentResponse>())!.Name);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (endWithEdit.StatusCode, await endWithEdit.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, endWithDelete.StatusCode);
        Assert.NotNull((await endWithDelete.Content.ReadFromJsonAsync<CommitmentResponse>())!.LastDueDate);
    }

    // A17: Die letzte Zahlung zu setzen oder zu leeren beendet einen Vertrag oder holt ihn zurück; das ist
    // dasselbe wie Beenden und braucht deshalb Löschen. Dieselbe letzte Zahlung noch einmal zu schicken, ändert nichts.
    [Fact]
    public async Task Setting_or_clearing_the_last_payment_of_annas_contract_needs_delete()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var rent = await RentOfAsync(anna);
        var ended = await RentOfAsync(anna);
        var lastDue = new DateOnly(2027, 3, 1);
        (await client.SendAsync(anna, HttpMethod.Patch, $"{Commitments}/{ended.Id}", new UpdateCommitmentRequest(LastDueDate: lastDue))).EnsureSuccessStatusCode();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Edit));

        var setWithEdit = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rent.Id}", new UpdateCommitmentRequest(Name: "Kaltmiete", LastDueDate: lastDue));
        var clearWithEdit = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{ended.Id}", new UpdateCommitmentRequest(ClearLastDueDate: true));
        var sameWithEdit = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{ended.Id}", new UpdateCommitmentRequest(Name: "Warmmiete", LastDueDate: lastDue));
        var storedWithEdit = await fixture.Factory.WithDbAsync(db => db.Commitments.AsNoTracking().SingleAsync(c => c.Id == rent.Id));
        var endedWithEdit = await fixture.Factory.WithDbAsync(db => db.Commitments.AsNoTracking().SingleAsync(c => c.Id == ended.Id));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Delete));
        var setWithDelete = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rent.Id}", new UpdateCommitmentRequest(LastDueDate: lastDue));
        var clearWithDelete = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{ended.Id}", new UpdateCommitmentRequest(ClearLastDueDate: true));

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (setWithEdit.StatusCode, await setWithEdit.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (clearWithEdit.StatusCode, await clearWithEdit.ReadCodeAsync()));
        Assert.Equal(("Miete", (DateOnly?)null), (storedWithEdit.Name, storedWithEdit.LastDueDate));
        Assert.Equal(HttpStatusCode.OK, sameWithEdit.StatusCode);
        Assert.Equal(("Warmmiete", (DateOnly?)lastDue), (endedWithEdit.Name, endedWithEdit.LastDueDate));
        Assert.Equal(lastDue, (await setWithDelete.Content.ReadFromJsonAsync<CommitmentResponse>())!.LastDueDate);
        Assert.Null((await clearWithDelete.Content.ReadFromJsonAsync<CommitmentResponse>())!.LastDueDate);
    }

    // A9
    [Fact]
    public async Task Claras_contract_is_not_found_for_ben_and_stays_as_it_is()
    {
        var (_, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var rent = await RentOfAsync(clara);

        var list = await client.SendAsync(ben, HttpMethod.Get, $"{Commitments}?owner={clara.Id}");
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rent.Id}", new UpdateCommitmentRequest(Name: "Meins"));
        var end = await client.SendAsync(ben, HttpMethod.Post, $"{Commitments}/{rent.Id}/end");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (list.StatusCode, await list.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.CommitmentNotFound), (change.StatusCode, await change.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.CommitmentNotFound), (end.StatusCode, await end.ReadCodeAsync()));
        var stored = await fixture.Factory.WithDbAsync(db => db.Commitments.AsNoTracking().SingleAsync(c => c.Id == rent.Id));
        Assert.Equal(("Miete", (DateOnly?)null), (stored.Name, stored.LastDueDate));
    }
}
