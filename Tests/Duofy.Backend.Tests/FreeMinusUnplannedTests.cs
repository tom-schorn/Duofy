using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Verplanbar zieht die gebuchten Ausgaben ohne Posten ab und rechnet die Einnahmen ohne Posten dazu (Spec 93, Toms
/// Entscheidung vom 04.11.2026: "Verplanbar zieht sie ab"). Zeilen auf einem Posten, Umbuchungen und die Buchungen der
/// Überträge zählen nicht noch einmal. Überall, wo Verplanbar steht, dieselbe Zahl. Die Uhr steht am 20. Oktober 2026,
/// gebucht wird in den November.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FreeMinusUnplannedTests : IDisposable
{
    private const string Plans = "/api/v1/plans";
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public FreeMinusUnplannedTests(ApiFixture fixture)
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

    private async Task<Guid> GiroAsync(Person person)
    {
        var listed = await client.SendAsync(person, HttpMethod.Get, "/api/v1/accounts");
        return (await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!.Single(a => a.IsDefault).Id;
    }

    /// <summary>Plant den November mit 2000 Einnahmen und 1500 Miete; gibt die Kennung der Miete zurück.</summary>
    private async Task<Guid> PlanNovemberAsync(Person person)
    {
        await PositionAsync(person, "Gehalt", 2000m, PotKeys.Income, "income.earned");
        return await PositionAsync(person, "Miete", 1500m, PotKeys.Needs, "housing.rent");
    }

    private async Task<Guid> PositionAsync(Person person, string label, decimal amount, string pot, string category)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/positions",
            new CreatePositionRequest(label, amount, 10, CategoryCatalog.IdFor(category), pot));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await created.Content.ReadFromJsonAsync<PlanPositionResponse>())!.Id;
    }

    /// <summary>Bucht in den November: eine Ausgabe oder Einnahme mit einer Zeile, auf einem Posten oder ohne.</summary>
    private async Task BookAsync(Person person, string kind, decimal amount, string pot, string category, Guid? position = null)
    {
        var booked = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            kind, await GiroAsync(person), null, Today, amount, null,
            [new SaveBookingLine(amount, pot, CategoryCatalog.IdFor(category), position)], 2026, 11));
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
    }

    private Task GroceriesAsync(Person person, decimal amount) =>
        BookAsync(person, BookingKinds.Expense, amount, PotKeys.Needs, "household.groceries");

    private async Task<T> GetAsync<T>(Person viewer, string path)
    {
        var answer = await client.SendAsync(viewer, HttpMethod.Get, path);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        return (await answer.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<decimal> FreeAsync(Person viewer, int month = 11) =>
        (await GetAsync<PlanResponse>(viewer, $"{Plans}/2026/{month}")).Overview!.Free;

    private async Task<decimal?> ListedFreeAsync(Person viewer, int month = 11) =>
        (await GetAsync<List<PlanMonthResponse>>(viewer, Plans)).Single(m => (m.Year, m.Month) == (2026, month)).Free;

    private async Task<decimal> HouseholdFreeAsync(Person viewer, Guid household) =>
        (await GetAsync<HouseholdPlanResponse>(viewer, $"/api/v1/households/{household}/plans/2026/11")).Free;

    private async Task<decimal?> HouseholdListedFreeAsync(Person viewer, Guid household) =>
        (await GetAsync<List<HouseholdPlanMonth>>(viewer, $"/api/v1/households/{household}/plans")).Single(m => (m.Year, m.Month) == (2026, 11)).Free;

    // A1 und A2: 2000 rein, 1500 Miete, 100 Lebensmittel ohne Posten; der Monat und die Liste der Monate zeigen 400.
    [Fact]
    public async Task A1_A2_an_expense_without_a_position_lowers_free_in_the_month_and_in_the_list()
    {
        var person = await client.PersonAsync("Anna");
        await PlanNovemberAsync(person);
        Assert.Equal(500m, await FreeAsync(person));

        await GroceriesAsync(person, 100m);

        Assert.Equal(400m, await FreeAsync(person));
        Assert.Equal(400m, await ListedFreeAsync(person));
    }

    // A3: Im Haushalt zählt die ungeplante Ausgabe eines Mitglieds, im Plan und in der Liste seiner Monate.
    [Fact]
    public async Task A3_the_household_plan_subtracts_the_unplanned_expense_of_a_member()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PlanNovemberAsync(anna);
        await PlanNovemberAsync(ben);
        var before = await HouseholdFreeAsync(anna, household);

        await GroceriesAsync(anna, 50m);

        Assert.Equal(before - 50m, await HouseholdFreeAsync(anna, household));
        Assert.Equal(before - 50m, await HouseholdListedFreeAsync(anna, household));
    }

    // Das Ungeplante aller Mitglieder steht ohnehin für alle im Haushaltsplan (Spec 46, R4); deshalb zählt es in Verplanbar
    // auch ohne Freigabe der Buchungen, und die Zahl hängt davon nicht ab. Nur der Übertrag bleibt an die Freigabe gebunden.
    [Fact]
    public async Task A3_the_household_sum_counts_the_unplanned_lines_of_every_member_with_and_without_the_booking_grant()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PlanNovemberAsync(anna);
        await PlanNovemberAsync(ben);
        var before = await HouseholdFreeAsync(ben, household);

        await GroceriesAsync(anna, 50m);

        Assert.Equal(before - 50m, await HouseholdFreeAsync(ben, household));
        Assert.Equal(before - 50m, await HouseholdListedFreeAsync(ben, household));

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        Assert.Equal(before - 50m, await HouseholdFreeAsync(ben, household));
        Assert.Equal(before - 50m, await HouseholdListedFreeAsync(ben, household));
    }

    // A4: Eine Zeile auf einem Posten, eine Umbuchung zwischen eigenen Konten und ein Übertrag ändern Verplanbar nur
    // nach der bisherigen Regel: der Übertrag kommt dazu, sonst nichts.
    [Fact]
    public async Task A4_lines_on_a_position_transfers_and_the_carry_over_do_not_count_twice()
    {
        var person = await client.PersonAsync("Anna");
        var rent = await PlanNovemberAsync(person);
        var giro = await GiroAsync(person);
        var cash = await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Bar", AccountTypes.Cash, 0m, new DateOnly(2026, 1, 1)));
        Assert.True(cash.StatusCode == HttpStatusCode.Created, await cash.Content.ReadAsStringAsync());
        var cashId = (await cash.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
        // Der Rest im Oktober für den Übertrag; er steht im Oktober und zählt im November nicht als ungeplant.
        var rest = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Income, giro, null, Today, 230m, null,
            [new SaveBookingLine(230m, PotKeys.Income, CategoryCatalog.IdFor("income.earned"))]));
        Assert.True(rest.StatusCode == HttpStatusCode.Created, await rest.Content.ReadAsStringAsync());
        Assert.Equal(500m, await FreeAsync(person));

        await BookAsync(person, BookingKinds.Expense, 1500m, PotKeys.Needs, "housing.rent", rent);
        var transfer = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Transfer, giro, cashId, Today, 100m, null, [new SaveBookingLine(100m, PotKeys.Needs, null)], 2026, 11,
            TargetPot: PotKeys.Wants));
        Assert.True(transfer.StatusCode == HttpStatusCode.Created, await transfer.Content.ReadAsStringAsync());
        Assert.Equal(500m, await FreeAsync(person));
        Assert.Equal(500m, await ListedFreeAsync(person));

        var octoberBefore = await FreeAsync(person, 10);
        var carried = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/carry-over", new CreateCarryOverRequest(230m));
        Assert.True(carried.StatusCode == HttpStatusCode.Created, await carried.Content.ReadAsStringAsync());

        Assert.Equal(730m, await FreeAsync(person));
        Assert.Equal(730m, await ListedFreeAsync(person));
        // "Übertrag nach November" ist eine Ausgabe im Oktober ohne Posten; sie senkt den Oktober nicht.
        Assert.Equal(octoberBefore, await FreeAsync(person, 10));
        Assert.Equal(octoberBefore, await ListedFreeAsync(person, 10));
    }

    // A5: Eine Einnahme von 20 ohne Posten, etwa Zinsen, hebt Verplanbar um 20.
    [Fact]
    public async Task A5_an_income_without_a_position_raises_free()
    {
        var person = await client.PersonAsync("Anna");
        await PlanNovemberAsync(person);

        await BookAsync(person, BookingKinds.Income, 20m, PotKeys.Income, "income.earned");

        Assert.Equal(520m, await FreeAsync(person));
        Assert.Equal(520m, await ListedFreeAsync(person));
    }

    // A8 (R7): Jeder Posten zählt mit dem Geplanten, oder mit dem Gebuchten, wenn mehr gebucht ist; Ausgaben und Einnahmen,
    // offen oder abgehakt. Toms Beispiele vom 04.11.2026: HP 4,48 geplant und 5,48 gebucht zählt 5,48; Lebensmittel 600
    // geplant, mit 426 gebucht abgehakt, zählt 600; eine Einnahme 267 geplant und 300 gebucht zählt 300.
    [Fact]
    public async Task A8_each_position_counts_with_the_planned_or_the_higher_booked_amount_in_the_month_and_in_the_list()
    {
        var person = await client.PersonAsync("Anna");
        await PlanNovemberAsync(person);
        var hp = await PositionAsync(person, "HP", 4.48m, PotKeys.Needs, "household.groceries");
        var groceries = await PositionAsync(person, "Lebensmittel", 600m, PotKeys.Needs, "household.groceries");
        var childBenefit = await PositionAsync(person, "Kindergeld", 267m, PotKeys.Income, "income.earned");
        // 2000 + 267 − 1500 − 4,48 − 600
        Assert.Equal(162.52m, await FreeAsync(person));

        await BookAsync(person, BookingKinds.Expense, 5.48m, PotKeys.Needs, "household.groceries", hp);
        await BookAsync(person, BookingKinds.Expense, 426m, PotKeys.Needs, "household.groceries", groceries);
        var ticked = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/positions/{groceries}/tick", new TickPositionRequest(Amount: 0m));
        Assert.True(ticked.StatusCode == HttpStatusCode.OK, await ticked.Content.ReadAsStringAsync());
        await BookAsync(person, BookingKinds.Income, 300m, PotKeys.Income, "income.earned", childBenefit);

        // 2000 + 300 − 1500 − 5,48 − 600
        Assert.Equal(194.52m, await FreeAsync(person));
        Assert.Equal(194.52m, await ListedFreeAsync(person));
    }

    // A8 (R7): Eine Einnahme mit weniger Gebuchtem zählt den Plan, und der Haushalt zählt mit derselben Regel.
    [Fact]
    public async Task A8_the_household_counts_the_higher_booked_amount_and_an_income_with_less_booked_keeps_the_plan()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PlanNovemberAsync(anna);
        await PlanNovemberAsync(ben);
        var hp = await PositionAsync(anna, "HP", 4.48m, PotKeys.Needs, "household.groceries");
        var childBenefit = await PositionAsync(anna, "Kindergeld", 267m, PotKeys.Income, "income.earned");
        var before = await HouseholdFreeAsync(ben, household);
        var annaBefore = await FreeAsync(anna);

        await BookAsync(anna, BookingKinds.Expense, 5.48m, PotKeys.Needs, "household.groceries", hp);
        await BookAsync(anna, BookingKinds.Income, 100m, PotKeys.Income, "income.earned", childBenefit);

        Assert.Equal(annaBefore - 1m, await FreeAsync(anna));
        Assert.Equal(before - 1m, await HouseholdFreeAsync(ben, household));
        Assert.Equal(before - 1m, await HouseholdListedFreeAsync(ben, household));
    }
}
