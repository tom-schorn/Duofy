using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Der Frei-Betrag enthält den Übertrag aus dem Vormonat (Toms Entscheidung vom 1.11.2026, Spec 66, R9): geplante
/// Einnahmen minus geplante Ausgaben plus Übertrag. Fest, Quoten, Töpfe und gebuchte Summen bleiben ohne ihn. Wer die
/// Buchungen einer Person nicht sieht, bekommt ihren Frei-Betrag ohne ihn, auch als Anteil der Haushaltssumme, denn dort
/// sind alle Posten zu sehen und der Betrag ließe sich sonst herausrechnen. Die Uhr steht fest am 20. Oktober 2026.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FreeWithCarryOverTests : IDisposable
{
    private const string Plans = "/api/v1/plans";

    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public FreeWithCarryOverTests(ApiFixture fixture)
    {
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(clock)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    /// <summary>Bucht im Oktober den Rest und plant den November mit <paramref name="income"/> und <paramref name="expenses"/>.</summary>
    private async Task PrepareAsync(Person person, decimal rest, decimal income, decimal expenses)
    {
        var listed = await client.SendAsync(person, HttpMethod.Get, "/api/v1/accounts");
        var giro = Assert.Single((await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!).Id;
        var (kind, pot, category) = rest >= 0m
            ? (BookingKinds.Income, PotKeys.Income, "income.earned")
            : (BookingKinds.Expense, PotKeys.Needs, "household.groceries");
        var booked = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            kind, giro, null, new DateOnly(2026, 10, 20), Math.Abs(rest), null,
            [new SaveBookingLine(Math.Abs(rest), pot, CategoryCatalog.IdFor(category))]));
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());

        await PositionAsync(person, "Gehalt", income, PotKeys.Income, "income.earned");
        await PositionAsync(person, "Miete", expenses, PotKeys.Needs, "housing.rent");
    }

    private async Task PositionAsync(Person person, string label, decimal amount, string pot, string category)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/positions",
            new CreatePositionRequest(label, amount, 10, CategoryCatalog.IdFor(category), pot));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
    }

    private async Task CarryOverAsync(Person person, int year, int month, decimal amount)
    {
        var answer = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/{year}/{month}/carry-over", new CreateCarryOverRequest(amount));
        Assert.True(answer.StatusCode == HttpStatusCode.Created, await answer.Content.ReadAsStringAsync());
    }

    private async Task<T> GetAsync<T>(Person viewer, string path)
    {
        var answer = await client.SendAsync(viewer, HttpMethod.Get, path);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        return (await answer.Content.ReadFromJsonAsync<T>())!;
    }

    private Task<PlanResponse> PlanAsync(Person viewer, int month = 11, string query = "") =>
        GetAsync<PlanResponse>(viewer, $"{Plans}/2026/{month}{query}");

    private async Task<PlanMonthResponse> ListedAsync(Person viewer, int month = 11, string query = "") =>
        (await GetAsync<List<PlanMonthResponse>>(viewer, Plans + query)).Single(m => m.Month == month);

    private Task<HouseholdPlanResponse> HouseholdPlanAsync(Person viewer, Guid household) =>
        GetAsync<HouseholdPlanResponse>(viewer, $"/api/v1/households/{household}/plans/2026/11");

    private async Task<HouseholdPlanMonth> HouseholdListedAsync(Person viewer, Guid household) =>
        (await GetAsync<List<HouseholdPlanMonth>>(viewer, $"/api/v1/households/{household}/plans")).Single(m => (m.Year, m.Month) == (2026, 11));

    // Toms Beispiel: 2500 geplant rein, 2200 geplant raus, Übertrag 230 oder -150.
    [Theory]
    [InlineData(230.00, 530.00)]
    [InlineData(-150.00, 150.00)]
    public async Task Free_contains_the_carry_over_in_the_month_and_in_the_list_of_months(double rest, double free)
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, (decimal)rest, 2500m, 2200m);

        await CarryOverAsync(person, 2026, 11, (decimal)rest);

        Assert.Equal((decimal)free, (await PlanAsync(person)).Overview!.Free);
        var listed = await ListedAsync(person);
        Assert.Equal((decimal)free, listed.Free);
        Assert.Equal((decimal)rest, listed.CarryOver);
    }

    [Fact]
    public async Task Without_a_carry_over_free_stays_planned_income_minus_planned_expenses()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);

        var plan = await PlanAsync(person);

        Assert.Equal(300m, plan.Overview!.Free);
        var listed = await ListedAsync(person);
        Assert.Equal(300m, listed.Free);
        Assert.Null(listed.CarryOver);
    }

    [Fact]
    public async Task Removing_the_carry_over_takes_it_out_of_free_again()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);
        await CarryOverAsync(person, 2026, 11, 230m);

        var removed = await client.SendAsync(person, HttpMethod.Delete, $"{Plans}/2026/11/carry-over");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(300m, (await PlanAsync(person)).Overview!.Free);
        Assert.Equal(300m, (await ListedAsync(person)).Free);
    }

    // Ein Rest von 0 bucht kein Paar: Es gibt keinen Betrag und keinen Hinweis.
    [Fact]
    public async Task A_rest_of_zero_changes_nothing()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);

        await CarryOverAsync(person, 2026, 11, 0m);

        Assert.Equal(300m, (await PlanAsync(person)).Overview!.Free);
        Assert.Null((await ListedAsync(person)).CarryOver);
    }

    // Keine Doppelzählung: Nur Frei ändert sich, alle fälligen und gebuchten Zahlen bleiben ohne Übertrag.
    [Fact]
    public async Task Fixed_income_quotas_pots_and_booked_sums_stay_without_the_carry_over()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);
        var before = await PlanAsync(person);

        await CarryOverAsync(person, 2026, 11, 230m);

        var after = await PlanAsync(person);
        Assert.Equal(before.Overview!.Free + 230m, after.Overview!.Free);
        Assert.Equal((before.Overview.Income, before.Overview.Fixed), (after.Overview.Income, after.Overview.Fixed));
        Assert.Equal((before.PlannedIncome, before.PlannedExpenses), (after.PlannedIncome, after.PlannedExpenses));
        Assert.Equal((before.BookedIncome, before.BookedExpenses), (after.BookedIncome, after.BookedExpenses));
        Assert.Equal(before.Quotas, after.Quotas);
        Assert.Equal(before.Overview.Pots, after.Overview.Pots);
    }

    // Der Vormonat bekommt nichts abgezogen: "Übertrag nach ..." gehört nicht in seinen Frei-Betrag.
    [Fact]
    public async Task The_month_the_rest_leaves_keeps_its_free_amount()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);
        var before = (await PlanAsync(person, 10)).Overview!.Free;

        await CarryOverAsync(person, 2026, 11, 230m);

        Assert.Equal(before, (await PlanAsync(person, 10)).Overview!.Free);
        var listed = await ListedAsync(person, 10);
        Assert.Equal(before, listed.Free);
        Assert.Null(listed.CarryOver);
    }

    // Über zwei Monate: Der Vorschlag rechnet aus Buchungen, der Frei-Betrag fließt nicht in ihn ein.
    [Fact]
    public async Task The_suggestion_for_the_next_carry_over_does_not_follow_the_free_amount()
    {
        var person = await client.PersonAsync("Anna");
        await PrepareAsync(person, 230m, 2500m, 2200m);
        await CarryOverAsync(person, 2026, 11, 230m);
        Assert.Equal(530m, (await PlanAsync(person)).Overview!.Free);

        // Der Rest bleibt 230, nicht 530: Geplantes ist kein Geld, das da ist.
        Assert.Equal(230m, (await PlanAsync(person, 12)).CarryOver.Suggested);

        await CarryOverAsync(person, 2026, 12, 230m);

        Assert.Equal(230m, (await PlanAsync(person, 12)).Overview!.Free);
        Assert.Equal(230m, (await GetAsync<PlanResponse>(person, $"{Plans}/2027/1")).CarryOver.Suggested);
        // Der November verliert nichts durch den weitergereichten Übertrag.
        Assert.Equal(530m, (await PlanAsync(person)).Overview!.Free);
    }

    // Keine neue Offenlegung: Ohne Freigabe der Buchungen weder der Betrag noch der Hinweis.
    [Fact]
    public async Task Someone_without_the_bookings_gets_free_without_the_carry_over_of_that_person()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await PrepareAsync(anna, 230m, 2500m, 2200m);
        await CarryOverAsync(anna, 2026, 11, 230m);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var query = $"?owner={anna.Id}";

        Assert.Equal(300m, (await PlanAsync(ben, query: query)).Overview!.Free);
        var hidden = await ListedAsync(ben, query: query);
        Assert.Equal((300m, (decimal?)null), (hidden.Free, hidden.CarryOver));

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));

        Assert.Equal(530m, (await PlanAsync(ben, query: query)).Overview!.Free);
        var shown = await ListedAsync(ben, query: query);
        Assert.Equal((530m, (decimal?)230m), (shown.Free, shown.CarryOver));
    }

    // Im Haushalt sieht jede Person alle Posten: Ein fremder Übertrag in der Summe ließe sich herausrechnen.
    [Fact]
    public async Task The_household_sum_contains_only_the_carry_overs_of_members_whose_bookings_the_viewer_sees()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PrepareAsync(anna, 230m, 2500m, 2200m);
        await PrepareAsync(ben, 100m, 1000m, 900m);
        await CarryOverAsync(anna, 2026, 11, 230m);
        await CarryOverAsync(ben, 2026, 11, 100m);

        // Geplant frei sind 300 und 100, zusammen 400; jede Person sieht nur den eigenen Übertrag.
        var seenByBen = await HouseholdPlanAsync(ben, household);
        var seenByAnna = await HouseholdPlanAsync(anna, household);
        Assert.Equal(500m, seenByBen.Free);
        Assert.Equal(630m, seenByAnna.Free);
        var listedByBen = await HouseholdListedAsync(ben, household);
        Assert.Equal((500m, (decimal?)100m), (listedByBen.Free, listedByBen.CarryOver));
        var listedByAnna = await HouseholdListedAsync(anna, household);
        Assert.Equal((630m, (decimal?)230m), (listedByAnna.Free, listedByAnna.CarryOver));

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        var granted = await HouseholdPlanAsync(ben, household);
        Assert.Equal(730m, granted.Free);
        var listed = await HouseholdListedAsync(ben, household);
        Assert.Equal((730m, (decimal?)330m), (listed.Free, listed.CarryOver));
        // Alle anderen Zahlen hängen nicht davon ab, wer schaut.
        Assert.Equal(
            (seenByBen.Income, seenByBen.Fixed, seenByBen.Unpaid, seenByBen.Unplanned),
            (granted.Income, granted.Fixed, granted.Unpaid, granted.Unplanned));
        Assert.Equal(seenByBen.Pots, granted.Pots);
        Assert.Equal(seenByBen.Quotas, granted.Quotas);
    }
}
