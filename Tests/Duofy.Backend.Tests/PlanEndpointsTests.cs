using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Verplanbar zählt Umbuchungen über die Grenze zwischen verfügbaren und nicht verfügbaren Konten (Spec 98): Geld, das vom
/// Sparkonto zurückgeholt wird, hebt es, Geld, das ohne Posten zurückgelegt wird, senkt es. Monat, Liste der Monate und
/// Haushaltsplan zeigen dieselbe Zahl. Die Uhr steht am 20. Oktober 2026, gebucht wird in den November.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PlanEndpointsTests : IDisposable
{
    private const string Plans = "/api/v1/plans";
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public PlanEndpointsTests(ApiFixture fixture)
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

    private async Task<Guid> AccountAsync(Person person, string name, bool available)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest(name, available ? AccountTypes.Checking : AccountTypes.Savings, 0m, new DateOnly(2026, 1, 1), CountsAsAvailable: available));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await created.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    /// <summary>Plant den November mit 2000 Einnahmen und 1500 Miete; gibt die Kennung der Miete zurück.</summary>
    private async Task PlanNovemberAsync(Person person)
    {
        foreach (var (label, amount, pot, category) in new[]
        {
            ("Gehalt", 2000m, PotKeys.Income, "income.earned"),
            ("Miete", 1500m, PotKeys.Needs, "housing.rent"),
        })
        {
            var created = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/positions",
                new CreatePositionRequest(label, amount, 10, CategoryCatalog.IdFor(category), pot));
            Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        }
    }

    private async Task TransferAsync(Person person, Guid from, Guid to, decimal amount)
    {
        var booked = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Transfer, from, to, Today, amount, null, [new SaveBookingLine(amount, PotKeys.Savings, null)], 2026, 11,
            TargetPot: PotKeys.Savings));
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
    }

    private async Task<T> GetAsync<T>(Person viewer, string path)
    {
        var answer = await client.SendAsync(viewer, HttpMethod.Get, path);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        return (await answer.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<decimal> FreeAsync(Person viewer) =>
        (await GetAsync<PlanResponse>(viewer, $"{Plans}/2026/11")).Overview!.Free;

    private async Task<decimal?> ListedFreeAsync(Person viewer) =>
        (await GetAsync<List<PlanMonthResponse>>(viewer, Plans)).Single(m => (m.Year, m.Month) == (2026, 11)).Free;

    private async Task<decimal> HouseholdFreeAsync(Person viewer, Guid household) =>
        (await GetAsync<HouseholdPlanResponse>(viewer, $"/api/v1/households/{household}/plans/2026/11")).Free;

    private async Task<decimal?> HouseholdListedFreeAsync(Person viewer, Guid household) =>
        (await GetAsync<List<HouseholdPlanMonth>>(viewer, $"/api/v1/households/{household}/plans")).Single(m => (m.Year, m.Month) == (2026, 11)).Free;

    // A4: Zurückgeholt, zurückgelegt und gleichseitig: Monat, Liste der Monate und Haushalt zeigen dieselbe Zahl.
    [Fact]
    public async Task A4_month_list_month_and_household_show_the_same_free_with_transfers()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PlanNovemberAsync(anna);
        await PlanNovemberAsync(ben);
        var giro = await GiroAsync(anna);
        var card = await AccountAsync(anna, "Kreditkarte", available: true);
        var depot = await AccountAsync(anna, "Depot-Verrechnung", available: false);
        var tagesgeld = await AccountAsync(anna, "Tagesgeld-Verrechnung", available: false);
        var before = await FreeAsync(anna);
        var householdBefore = await HouseholdFreeAsync(ben, household);
        var householdListedBefore = await HouseholdListedFreeAsync(ben, household);

        await TransferAsync(anna, depot, giro, 270m);      // zurückgeholt: +270
        await TransferAsync(anna, giro, tagesgeld, 120m);  // zurückgelegt ohne Posten: -120
        await TransferAsync(anna, giro, card, 90m);        // beide verfügbar: 0
        await TransferAsync(anna, depot, tagesgeld, 160m); // beide nicht verfügbar: 0

        Assert.Equal(before + 150m, await FreeAsync(anna));
        Assert.Equal(before + 150m, await ListedFreeAsync(anna));
        // Ohne Freigabe der Buchungen zählen Annas Umbuchungen für Ben nicht, im Plan wie in der Liste.
        Assert.Equal(householdBefore, await HouseholdFreeAsync(ben, household));
        Assert.Equal(householdListedBefore, await HouseholdListedFreeAsync(ben, household));
        Assert.Equal(householdBefore + 150m, await HouseholdFreeAsync(anna, household));
        Assert.Equal(householdBefore + 150m, await HouseholdListedFreeAsync(anna, household));

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        Assert.Equal(householdBefore + 150m, await HouseholdFreeAsync(ben, household));
        Assert.Equal(householdBefore + 150m, await HouseholdListedFreeAsync(ben, household));
    }
}
