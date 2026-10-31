using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Der Monatsplan einer anderen Person mit Freigabe (Spec 68, R4, R5, R9).</summary>
[Collection(ApiCollection.Name)]
public class GrantedPlanTests
{
    private const string November = "/api/v1/plans/2026/11";

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public GrantedPlanTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static CreatePositionRequest Gift() =>
        new("Geschenk", 30.00m, 15, CategoryCatalog.IdFor("household.groceries"), PotKeys.Needs);

    private async Task GiroOfAsync(Person owner) =>
        (await client.SendAsync(owner, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)))).EnsureSuccessStatusCode();

    private async Task<PlanPositionResponse> PositionOfAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, $"{November}/positions", Gift());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
    }

    private async Task<PlanResponse> PlanOfAsync(Person owner)
    {
        var response = await client.SendAsync(owner, HttpMethod.Get, November);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlanResponse>())!;
    }

    // A7
    [Fact]
    public async Task With_plan_edit_ben_opens_annas_november_and_adds_a_position_but_cannot_tick_without_the_book()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await GiroOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit));

        var open = await client.SendAsync(ben, HttpMethod.Get, $"{November}?owner={anna.Id}");
        var add = await client.SendAsync(ben, HttpMethod.Post, $"{November}/positions?owner={anna.Id}", Gift());
        var added = (await add.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
        var tick = await client.SendAsync(ben, HttpMethod.Post, $"{November}/positions/{added.Id}/tick?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var annas = await PlanOfAsync(anna);
        Assert.Equal(annas.Id, (await open.Content.ReadFromJsonAsync<PlanResponse>())!.Id);
        Assert.Contains(annas.Positions, p => p.Id == added.Id && p.Label == "Geschenk");
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.PositionNotFound), (tick.StatusCode, await tick.ReadCodeAsync()));
        Assert.Null(annas.Positions.Single(p => p.Id == added.Id).DoneAt);
        Assert.False(await fixture.Factory.WithDbAsync(db => db.Bookings.AnyAsync(b => b.OwnerId == anna.Id)));
    }

    // R4: Mit Planung Ansehen fehlt jeder weitere Schritt.
    [Fact]
    public async Task With_plan_view_ben_sees_annas_month_but_adds_changes_and_deletes_nothing()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var position = await PositionOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var add = await client.SendAsync(ben, HttpMethod.Post, $"{November}/positions?owner={anna.Id}", Gift());
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{November}/positions/{position.Id}?owner={anna.Id}", new UpdatePositionRequest(Label: "Anders"));
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{November}/positions/{position.Id}?owner={anna.Id}");

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (add.StatusCode, await add.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (change.StatusCode, await change.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (delete.StatusCode, await delete.ReadCodeAsync()));
        var positions = (await PlanOfAsync(anna)).Positions;
        Assert.Equal("Geschenk", Assert.Single(positions).Label);
    }

    // R4: Abhaken ist eine Frage der Buchungen; die Buchung gehört Anna.
    [Fact]
    public async Task With_the_book_ben_ticks_annas_position_and_takes_the_tick_back_only_with_delete()
    {
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await PositionOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.Edit, Accounts: AccessLevelKeys.View));

        var tick = await client.SendAsync(ben, HttpMethod.Post, $"{November}/positions/{position.Id}/tick?owner={anna.Id}");
        var untick = await client.SendAsync(ben, HttpMethod.Delete, $"{November}/positions/{position.Id}/tick?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);
        var ticked = (await tick.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
        Assert.NotNull(ticked.DoneAt);
        Assert.Equal(anna.Id, await fixture.Factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == ticked.TickBookingId).Select(b => b.OwnerId).SingleAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (untick.StatusCode, await untick.ReadCodeAsync()));
        Assert.NotNull(Assert.Single((await PlanOfAsync(anna)).Positions).DoneAt);
    }

    // R4: Die Bereiche hängen nicht voneinander ab. Wer nur die Planung sieht, sieht darin weder Buchungen
    // noch Kontostände noch Verträge.
    [Fact]
    public async Task With_plan_view_only_annas_month_shows_no_bookings_balances_or_contracts()
    {
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await PositionOfAsync(anna);
        (await client.SendAsync(anna, HttpMethod.Post, $"{November}/positions/{position.Id}/tick")).EnsureSuccessStatusCode();
        (await client.SendAsync(anna, HttpMethod.Post, "/api/v1/commitments", new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", 890.00m, 1, new DateOnly(2026, 11, 5), null,
            CategoryCatalog.IdFor("housing.rent"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder))).EnsureSuccessStatusCode();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var planOnly = (await (await client.SendAsync(ben, HttpMethod.Get, $"{November}?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View, Commitments: AccessLevelKeys.View));
        var everything = (await (await client.SendAsync(ben, HttpMethod.Get, $"{November}?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!;

        var hidden = Assert.Single(planOnly.Positions);
        Assert.Equal((0m, (Guid?)null), (hidden.Booked, hidden.TickBookingId));
        Assert.NotNull(hidden.DoneAt);
        Assert.Equal(0m, planOnly.BookedExpenses);
        Assert.Empty(planOnly.CarryOver.Balances);
        Assert.Empty(planOnly.Changes);
        var shown = Assert.Single(everything.Positions);
        Assert.Equal(30.00m, shown.Booked);
        Assert.Equal(30.00m, everything.BookedExpenses);
        Assert.NotEmpty(everything.CarryOver.Balances);
        Assert.NotEmpty(everything.Changes);
    }

    // R4: Der Übertrag legt Buchungen an und löscht sie; dafür braucht es die Buchungen, nicht nur den Plan.
    [Fact]
    public async Task The_carry_over_of_annas_month_needs_the_book_as_well()
    {
        // Ohne das erste Konto ist das Giro das Standardkonto; das erste Konto startet heute und trüge keinen Übertrag.
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete, Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View));

        var create = await client.SendAsync(ben, HttpMethod.Post, $"{November}/carry-over?owner={anna.Id}", new CreateCarryOverRequest(50.00m));
        (await client.SendAsync(anna, HttpMethod.Post, $"{November}/carry-over", new CreateCarryOverRequest(50.00m))).EnsureSuccessStatusCode();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Edit));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{November}/carry-over?owner={anna.Id}");

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (create.StatusCode, await create.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (delete.StatusCode, await delete.ReadCodeAsync()));
        Assert.Equal(2, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.OwnerId == anna.Id)));
        Assert.True((await PlanOfAsync(anna)).CarryOver.Exists);
    }

    // A19: Ohne Annas Konten ansehen zu dürfen, ist der Übertrag wie das Abhaken nicht zu finden, kein falscher Antrag.
    [Fact]
    public async Task A_carry_over_for_anna_without_seeing_her_accounts_is_not_found()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await GiroOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit, Book: AccessLevelKeys.Edit));

        var create = await client.SendAsync(ben, HttpMethod.Post, $"{November}/carry-over?owner={anna.Id}", new CreateCarryOverRequest(50.00m));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.AccountNotFound), (create.StatusCode, await create.ReadCodeAsync()));
        Assert.Equal(0, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.OwnerId == anna.Id)));
    }

    // A18: Übernehmen, das einen Posten entfernt, ist Löschen; ohne die Stufe Löschen wird gar nichts übernommen.
    [Fact]
    public async Task Applying_changes_that_remove_a_position_of_anna_needs_plan_delete()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await GiroOfAsync(anna);
        var rent = await ContractOfAsync(anna, "Miete");
        await PlanOfAsync(anna);
        (await client.SendAsync(anna, HttpMethod.Patch, $"/api/v1/commitments/{rent.Id}",
            new UpdateCommitmentRequest(LastDueDate: new DateOnly(2026, 10, 1)))).EnsureSuccessStatusCode();
        var gym = await ContractOfAsync(anna, "Fitness");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit, Commitments: AccessLevelKeys.View));

        var all = await client.SendAsync(ben, HttpMethod.Post, $"{November}/changes/apply?owner={anna.Id}", new ApplyPlanChangesRequest());
        var named = await client.SendAsync(ben, HttpMethod.Post, $"{November}/changes/apply?owner={anna.Id}",
            new ApplyPlanChangesRequest([new PlanChangeItem(null, gym.Id), new PlanChangeItem(Position(await PlanOfAsync(anna), rent.Id), rent.Id)]));
        var unchanged = await PlanOfAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete));
        var withDelete = await client.SendAsync(ben, HttpMethod.Post, $"{November}/changes/apply?owner={anna.Id}", new ApplyPlanChangesRequest());

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (all.StatusCode, await all.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (named.StatusCode, await named.ReadCodeAsync()));
        Assert.Contains(unchanged.Positions, p => p.CommitmentId == rent.Id);
        Assert.DoesNotContain(unchanged.Positions, p => p.CommitmentId == gym.Id);
        Assert.Equal(2, unchanged.Changes.Count);
        Assert.Equal(HttpStatusCode.OK, withDelete.StatusCode);
        var after = await PlanOfAsync(anna);
        Assert.DoesNotContain(after.Positions, p => p.CommitmentId == rent.Id);
        Assert.Contains(after.Positions, p => p.CommitmentId == gym.Id);
    }

    // A18: Nur einen neuen Vertrag zu übernehmen, entfernt nichts und geht mit Ändern.
    [Fact]
    public async Task Applying_only_a_new_contract_of_anna_works_with_plan_edit()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await GiroOfAsync(anna);
        await PlanOfAsync(anna);
        var gym = await ContractOfAsync(anna, "Fitness");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit, Commitments: AccessLevelKeys.View));

        var applied = await client.SendAsync(ben, HttpMethod.Post, $"{November}/changes/apply?owner={anna.Id}", new ApplyPlanChangesRequest());

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Contains((await PlanOfAsync(anna)).Positions, p => p.CommitmentId == gym.Id);
    }

    private async Task<CommitmentResponse> ContractOfAsync(Person owner, string name)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, "/api/v1/commitments", new CreateCommitmentRequest(
            CommitmentTypes.Contract, name, 50.00m, 1, new DateOnly(2026, 1, 1), null,
            CategoryCatalog.IdFor("household.groceries"), PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
    }

    private static Guid Position(PlanResponse plan, Guid commitmentId) => plan.Positions.Single(p => p.CommitmentId == commitmentId).Id;

    // A9
    [Fact]
    public async Task Claras_plan_and_positions_are_not_found_for_ben_and_stay_as_they_are()
    {
        var (_, ben) = await client.AnnaAndBenAsync();
        var clara = await client.FounderAsync("Clara");
        var position = await PositionOfAsync(clara);

        var open = await client.SendAsync(ben, HttpMethod.Get, $"{November}?owner={clara.Id}");
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{November}/positions/{position.Id}?owner={clara.Id}", new UpdatePositionRequest(Label: "Meins"));
        var changeOwn = await client.SendAsync(ben, HttpMethod.Patch, $"{November}/positions/{position.Id}", new UpdatePositionRequest(Label: "Meins"));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{November}/positions/{position.Id}?owner={clara.Id}");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (open.StatusCode, await open.ReadCodeAsync()));
        foreach (var answer in new[] { change, changeOwn, delete })
        {
            Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.PositionNotFound), (answer.StatusCode, await answer.ReadCodeAsync()));
        }
        Assert.Equal("Geschenk", Assert.Single((await PlanOfAsync(clara)).Positions).Label);
    }
}
