using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Api.Tests;

/// <summary>Einen Posten in den nächsten Monat verschieben und zurücknehmen (Spec 136, A1 bis A7).</summary>
[Collection(ApiCollection.Name)]
public class PlanPostponeTests
{
    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public PlanPostponeTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private static string Month(int year, int month) => $"/api/v1/plans/{year}/{month}";

    private async Task GiroOfAsync(Person owner) =>
        (await client.SendAsync(owner, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro", AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)))).EnsureSuccessStatusCode();

    private async Task<PlanPositionResponse> OneOffAsync(Person owner, int year, int month, string label = "GitHub", decimal amount = 4m, int day = 10, string pot = PotKeys.Wants)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, $"{Month(year, month)}/positions",
            new CreatePositionRequest(label, amount, day, CategoryCatalog.IdFor(pot == PotKeys.Income ? "income.earned" : "household.groceries"), pot));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
    }

    private async Task<PlanResponse> PlanAsync(Person owner, int year, int month)
    {
        var response = await client.SendAsync(owner, HttpMethod.Get, Month(year, month));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlanResponse>())!;
    }

    private Task<HttpResponseMessage> PostponeAsync(Person caller, int year, int month, Guid id, Guid? owner = null) =>
        client.SendAsync(caller, HttpMethod.Post, $"{Month(year, month)}/positions/{id}/postpone" + (owner is null ? "" : $"?owner={owner}"));

    private Task<HttpResponseMessage> UndoAsync(Person caller, int year, int month, Guid id, Guid? owner = null) =>
        client.SendAsync(caller, HttpMethod.Post, $"{Month(year, month)}/positions/{id}/postpone/undo" + (owner is null ? "" : $"?owner={owner}"));

    private async Task BookAsync(Person owner, Guid positionId, decimal amount, int year, int month)
    {
        var accounts = await (await client.SendAsync(owner, HttpMethod.Get, "/api/v1/accounts")).Content.ReadFromJsonAsync<List<AccountResponse>>();
        var response = await client.SendAsync(owner, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Expense, accounts![0].Id, null, new DateOnly(2026, 11, 5), amount, null,
            [new SaveBookingLine(amount, PotKeys.Wants, CategoryCatalog.IdFor("household.groceries"), positionId)], year, month));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // A1, R1, R2
    [Fact]
    public async Task Postponing_marks_the_old_position_and_adds_a_linked_copy_with_the_same_fields_to_the_next_month()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await OneOffAsync(anna, 2026, 11, day: 15);

        var response = await PostponeAsync(anna, 2026, 11, position.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var both = (await response.Content.ReadFromJsonAsync<PostponePositionResponse>())!;
        Assert.Equal(new PlanMonthRef(2026, 12), both.From.MovedToMonth);
        Assert.Equal(new PlanMonthRef(2026, 11), both.To.MovedFromMonth);
        var november = (await PlanAsync(anna, 2026, 11)).Positions.Single(p => p.Id == position.Id);
        var december = (await PlanAsync(anna, 2026, 12)).Positions.Single(p => p.Id == both.To.Id);
        Assert.Equal(new PlanMonthRef(2026, 12), november.MovedToMonth);
        Assert.True(december.ChangedByHand);
        Assert.Equal((position.Label, position.AmountPlanned, position.CategoryId, position.Pot, position.AccountId, position.PaymentMethod, 15),
            (december.Label, december.AmountPlanned, december.CategoryId, december.Pot, december.AccountId, december.PaymentMethod, december.DueDay));
    }

    // A1: Die Zahlen beider Monate
    [Fact]
    public async Task The_old_month_figures_leave_the_moved_position_out_and_the_next_month_counts_the_copy()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        await OneOffAsync(anna, 2026, 11, "Gehalt", 3000m, pot: PotKeys.Income);
        var github = await OneOffAsync(anna, 2026, 11);
        await OneOffAsync(anna, 2026, 12, "Gehalt", 3000m, pot: PotKeys.Income);
        var decemberBefore = (await PlanAsync(anna, 2026, 12)).Overview!.Free;

        (await PostponeAsync(anna, 2026, 11, github.Id)).EnsureSuccessStatusCode();

        var november = await PlanAsync(anna, 2026, 11);
        Assert.Equal(3000m, november.Overview!.Free);
        Assert.Equal(0m, november.Overview.Fixed);
        Assert.Equal(0m, november.PlannedExpenses);
        Assert.Equal(0m, november.Overview.Pots.Single(p => p.Pot == PotKeys.Wants).Planned);
        Assert.Equal(decemberBefore - 4m, (await PlanAsync(anna, 2026, 12)).Overview!.Free);
    }

    // Startseite und Plan-Übersicht
    [Fact]
    public async Task Month_list_and_overview_leave_the_moved_position_out_of_free_and_unpaid()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        await OneOffAsync(anna, 2026, 11, "Gehalt", 3000m, pot: PotKeys.Income);
        var github = await OneOffAsync(anna, 2026, 11);

        (await PostponeAsync(anna, 2026, 11, github.Id)).EnsureSuccessStatusCode();

        var months = (await (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans")).Content.ReadFromJsonAsync<List<PlanMonthResponse>>())!;
        Assert.Equal(3000m, months.Single(m => m.Month == 11).Free);
        var overview = (await (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/overview?year=2026")).Content.ReadFromJsonAsync<List<PlanOverviewMonthResponse>>())!;
        var nov = overview.Single(m => m.Month == 11);
        Assert.Equal(3000m, nov.Free);
        Assert.Equal(0m, nov.Unpaid);
        Assert.Equal(0m, nov.Pots.Single(p => p.Pot == PotKeys.Wants).Planned);
        Assert.Equal(4m, overview.Single(m => m.Month == 12).Unpaid);
    }

    // A2
    [Fact]
    public async Task A_position_due_on_the_31st_moves_into_a_30_day_month_due_on_the_30th()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await OneOffAsync(anna, 2026, 10, day: 31);

        var both = (await (await PostponeAsync(anna, 2026, 10, position.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!;

        Assert.Equal(30, both.To.DueDay);
    }

    // A3
    [Fact]
    public async Task A_ticked_position_a_position_with_bookings_and_an_already_moved_one_are_refused_and_nothing_changes()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var ticked = await OneOffAsync(anna, 2026, 11, "Getickt");
        (await client.SendAsync(anna, HttpMethod.Post, $"{Month(2026, 11)}/positions/{ticked.Id}/tick")).EnsureSuccessStatusCode();
        var booked = await OneOffAsync(anna, 2026, 11, "Gebucht");
        await BookAsync(anna, booked.Id, 2m, 2026, 11);
        var moved = await OneOffAsync(anna, 2026, 11, "Schon verschoben");
        (await PostponeAsync(anna, 2026, 11, moved.Id)).EnsureSuccessStatusCode();

        var a = await PostponeAsync(anna, 2026, 11, ticked.Id);
        var b = await PostponeAsync(anna, 2026, 11, booked.Id);
        var c = await PostponeAsync(anna, 2026, 11, moved.Id);

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionPaid), (a.StatusCode, await a.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionPaid), (b.StatusCode, await b.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionAlreadyMoved), (c.StatusCode, await c.ReadCodeAsync()));
        Assert.Single((await PlanAsync(anna, 2026, 12)).Positions);
    }

    // A3, A4
    [Fact]
    public async Task Without_plan_edit_postponing_is_refused_and_with_plan_edit_it_works_on_the_owners_plan()
    {
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await OneOffAsync(anna, 2026, 11);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var refused = await PostponeAsync(ben, 2026, 11, position.Id, anna.Id);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Edit));
        var allowed = await PostponeAsync(ben, 2026, 11, position.Id, anna.Id);

        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoEditGranted), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Single((await PlanAsync(anna, 2026, 12)).Positions);
        Assert.Empty((await PlanAsync(ben, 2026, 12)).Positions);
    }

    // A5
    [Fact]
    public async Task Undo_removes_the_copy_and_the_old_month_counts_the_position_again_but_a_booked_copy_stays()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        await OneOffAsync(anna, 2026, 11, "Gehalt", 3000m, pot: PotKeys.Income);
        var position = await OneOffAsync(anna, 2026, 11);
        var both = (await (await PostponeAsync(anna, 2026, 11, position.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!;

        await BookAsync(anna, both.To.Id, 1m, 2026, 12);
        var refused = await UndoAsync(anna, 2026, 11, position.Id);
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionCopyPaid), (refused.StatusCode, await refused.ReadCodeAsync()));

        var bookings = (await (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/bookings?year=2026&month=11")).Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings;
        var booking = bookings.Single(b => b.Lines.Any(l => l.PositionId == both.To.Id));
        (await client.SendAsync(anna, HttpMethod.Delete, $"/api/v1/bookings/{booking.Id}")).EnsureSuccessStatusCode();
        var undone = await UndoAsync(anna, 2026, 11, position.Id);

        Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
        Assert.DoesNotContain((await PlanAsync(anna, 2026, 12)).Positions, p => p.Id == both.To.Id);
        var november = await PlanAsync(anna, 2026, 11);
        Assert.Null(november.Positions.Single(p => p.Id == position.Id).MovedToMonth);
        Assert.Equal(2996m, november.Overview!.Free);
    }

    // A6
    [Fact]
    public async Task A_copy_can_be_moved_on_and_names_only_its_direct_origin_month()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await OneOffAsync(anna, 2026, 11);
        var december = (await (await PostponeAsync(anna, 2026, 11, position.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!.To;

        var onward = (await (await PostponeAsync(anna, 2026, 12, december.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!;

        Assert.Equal(new PlanMonthRef(2027, 1), onward.From.MovedToMonth);
        Assert.Equal(new PlanMonthRef(2026, 12), onward.To.MovedFromMonth);
    }

    private async Task<HttpResponseMessage> TryBookAsync(Person owner, Guid positionId, decimal amount, int year, int month)
    {
        var accounts = await (await client.SendAsync(owner, HttpMethod.Get, "/api/v1/accounts")).Content.ReadFromJsonAsync<List<AccountResponse>>();
        return await client.SendAsync(owner, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Expense, accounts![0].Id, null, new DateOnly(2026, 11, 5), amount, null,
            [new SaveBookingLine(amount, PotKeys.Wants, CategoryCatalog.IdFor("household.groceries"), positionId)], year, month));
    }

    private async Task<(PlanPositionResponse Old, PlanPositionResponse Copy)> MovedAsync(Person owner, int year = 2026, int month = 11)
    {
        var position = await OneOffAsync(owner, year, month);
        var both = (await (await PostponeAsync(owner, year, month, position.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!;
        return (both.From, both.To);
    }

    // Review M2: Geld auf einem verschobenen Posten verschwände aus allen Zahlen.
    [Fact]
    public async Task A_booking_line_on_a_moved_position_is_refused_with_409_and_books_nothing()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var (old, copy) = await MovedAsync(anna);

        var refused = await TryBookAsync(anna, old.Id, 2m, 2026, 11);
        var onCopy = await TryBookAsync(anna, copy.Id, 2m, 2026, 12);

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionMoved), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.Created, onCopy.StatusCode);
        var bookings = (await (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/bookings?year=2026&month=11")).Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings;
        Assert.DoesNotContain(bookings, b => b.Lines.Any(l => l.PositionId == old.Id));
    }

    // Review S1
    [Fact]
    public async Task A_moved_original_cannot_be_deleted_and_its_copy_keeps_the_origin()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var (old, copy) = await MovedAsync(anna);

        var refused = await client.SendAsync(anna, HttpMethod.Delete, $"{Month(2026, 11)}/positions/{old.Id}");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionMoved), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Contains((await PlanAsync(anna, 2026, 11)).Positions, p => p.Id == old.Id);
        Assert.Equal(new PlanMonthRef(2026, 11), (await PlanAsync(anna, 2026, 12)).Positions.Single(p => p.Id == copy.Id).MovedFromMonth);
    }

    // Review S1
    [Fact]
    public async Task Applying_contract_changes_skips_a_moved_original_and_refuses_it_when_picked()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var rent = await client.SendAsync(anna, HttpMethod.Post, "/api/v1/commitments", new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", 890m, 1, new DateOnly(2026, 1, 1), null, CategoryCatalog.IdFor("household.groceries"), PotKeys.Needs, PaymentMethod: PaymentMethods.Other));
        Assert.Equal(HttpStatusCode.Created, rent.StatusCode);
        var contract = (await rent.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        var position = Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);
        var both = (await (await PostponeAsync(anna, 2026, 11, position.Id)).Content.ReadFromJsonAsync<PostponePositionResponse>())!;
        (await client.SendAsync(anna, HttpMethod.Patch, $"/api/v1/commitments/{contract.Id}", new UpdateCommitmentRequest(Amount: 950m))).EnsureSuccessStatusCode();

        var all = await client.SendAsync(anna, HttpMethod.Post, $"{Month(2026, 11)}/changes/apply", new ApplyPlanChangesRequest());
        var picked = await client.SendAsync(anna, HttpMethod.Post, $"{Month(2026, 11)}/changes/apply", new ApplyPlanChangesRequest([new PlanChangeItem(position.Id, null)]));

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(890m, (await PlanAsync(anna, 2026, 11)).Positions.Single(p => p.Id == position.Id).AmountPlanned);
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionMoved), (picked.StatusCode, await picked.ReadCodeAsync()));
        Assert.Equal(new PlanMonthRef(2026, 11), (await PlanAsync(anna, 2026, 12)).Positions.Single(p => p.Id == both.To.Id).MovedFromMonth);
    }

    // Review S2
    [Fact]
    public async Task Ticking_a_moved_position_is_refused_with_409()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var (old, _) = await MovedAsync(anna);

        var refused = await client.SendAsync(anna, HttpMethod.Post, $"{Month(2026, 11)}/positions/{old.Id}/tick");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionMoved), (refused.StatusCode, await refused.ReadCodeAsync()));
    }

    // Review S2
    [Fact]
    public async Task A_refused_postpone_does_not_create_the_next_month()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var position = await OneOffAsync(anna, 2027, 3);
        (await client.SendAsync(anna, HttpMethod.Post, $"{Month(2027, 3)}/positions/{position.Id}/tick")).EnsureSuccessStatusCode();

        var refused = await PostponeAsync(anna, 2027, 3, position.Id);

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionPaid), (refused.StatusCode, await refused.ReadCodeAsync()));
        var exists = await fixture.Factory.WithDbAsync(db => db.Plans.AnyAsync(p => p.OwnerId == anna.Id && p.Year == 2027 && p.Month == 4));
        Assert.False(exists);
    }

    // Review S2, N5
    [Fact]
    public async Task Undo_is_refused_when_the_copy_is_ticked_and_with_its_own_code_when_the_copy_was_moved_on()
    {
        var (anna, _) = await client.AnnaAndBenAsync(fixture.Factory);
        await GiroOfAsync(anna);
        var (old, copy) = await MovedAsync(anna);
        (await client.SendAsync(anna, HttpMethod.Post, $"{Month(2026, 12)}/positions/{copy.Id}/tick")).EnsureSuccessStatusCode();
        var ticked = await UndoAsync(anna, 2026, 11, old.Id);
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionCopyPaid), (ticked.StatusCode, await ticked.ReadCodeAsync()));

        var (old2, copy2) = await MovedAsync(anna, 2026, 10);
        (await PostponeAsync(anna, 2026, 11, copy2.Id)).EnsureSuccessStatusCode();
        var movedOn = await UndoAsync(anna, 2026, 10, old2.Id);

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.PositionCopyMoved), (movedOn.StatusCode, await movedOn.ReadCodeAsync()));
    }
}
