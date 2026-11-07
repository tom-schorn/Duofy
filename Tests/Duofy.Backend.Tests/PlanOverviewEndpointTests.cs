using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Übersicht mit Zahlen (GET /api/v1/plans/overview, Issue #94, A10): ein Aufruf liefert für jeden vorhandenen Monat des
/// Jahres dieselben Zahlen wie der Monat selbst, dazu vorhandene künftige Monate; nur lesend, nur die eigenen Pläne, das
/// Jahr wird geprüft. Die Uhr steht am 20. Oktober 2026.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PlanOverviewEndpointTests : IDisposable
{
    private const string Overview = "/api/v1/plans/overview";

    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public PlanOverviewEndpointTests(ApiFixture fixture)
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

    private async Task<Guid> PositionAsync(Person person, int year, int month, string label, decimal amount, string pot, string category)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, $"/api/v1/plans/{year}/{month}/positions",
            new CreatePositionRequest(label, amount, 10, CategoryCatalog.IdFor(category), pot));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await created.Content.ReadFromJsonAsync<PlanPositionResponse>())!.Id;
    }

    private async Task BookAsync(Person person, int year, int month, decimal amount, string category, Guid? position)
    {
        var accounts = await client.SendAsync(person, HttpMethod.Get, "/api/v1/accounts");
        var giro = (await accounts.Content.ReadFromJsonAsync<List<AccountResponse>>())!.Single(a => a.IsDefault).Id;
        var booked = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            BookingKinds.Expense, giro, null, new DateOnly(2026, 10, 20), amount, null,
            [new SaveBookingLine(amount, PotKeys.Needs, CategoryCatalog.IdFor(category), position)], year, month));
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
    }

    private async Task<List<PlanOverviewMonthResponse>> OverviewOfAsync(Person viewer, string query)
    {
        var response = await client.SendAsync(viewer, HttpMethod.Get, Overview + query);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<PlanOverviewMonthResponse>>())!;
    }

    // A10: dieselben Zahlen wie der Monat, in einem Aufruf
    [Fact]
    public async Task A10_one_call_returns_the_same_figures_as_the_months()
    {
        var anna = await client.PersonAsync("Anna");
        await PositionAsync(anna, 2026, 9, "Gehalt", 3000m, PotKeys.Income, "income.earned");
        var rent = await PositionAsync(anna, 2026, 10, "Miete", 890m, PotKeys.Needs, "housing.rent");
        await PositionAsync(anna, 2026, 10, "Gehalt", 3000m, PotKeys.Income, "income.earned");
        await BookAsync(anna, 2026, 10, 880m, "housing.rent", rent);
        await BookAsync(anna, 2026, 10, 88.89m, "household.groceries", null);

        var months = await OverviewOfAsync(anna, "?year=2026");

        Assert.Equal([(2026, 10), (2026, 9)], months.Select(m => (m.Year, m.Month)));
        foreach (var month in months)
        {
            var plan = (await (await client.SendAsync(anna, HttpMethod.Get, $"/api/v1/plans/{month.Year}/{month.Month}"))
                .Content.ReadFromJsonAsync<PlanResponse>())!;
            Assert.Equal(plan.Overview!.Free, month.Free);
            Assert.Equal(plan.Overview.Pots, month.Pots);
        }
        var october = months[0];
        Assert.Equal(new PlanPotResponse(PotKeys.Needs, 890m, 968.89m), october.Pots.Single(p => p.Pot == PotKeys.Needs));
        Assert.False(october.CarriedIn);
    }

    // Review S1: Anstehend der Übersicht ist dieselbe Zahl wie im Monat (offener Rest der nicht abgehakten Posten), auch für eine
    // Person ohne Sicht auf die Buchungen: dort ist das Gebuchte leer, also alles Geplante offen. Die Oberfläche blendet sie dann aus.
    private static decimal UnpaidOf(PlanResponse plan) => plan.Positions
        .Where(p => p.Pot != PotKeys.Income && !p.PassThrough && p.DoneAt is null)
        .Sum(p => Math.Max(p.AmountPlanned - p.Booked, 0m));

    [Fact]
    public async Task S1_unpaid_matches_the_month_for_the_owner_and_for_a_viewer_without_the_bookings()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var rent = await PositionAsync(anna, 2026, 10, "Miete", 890m, PotKeys.Needs, "housing.rent");
        await BookAsync(anna, 2026, 10, 880m, "housing.rent", rent);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var own = (await OverviewOfAsync(anna, "?year=2026")).Single();
        var ownPlan = (await (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/10")).Content.ReadFromJsonAsync<PlanResponse>())!;
        var seen = (await OverviewOfAsync(ben, $"?year=2026&owner={anna.Id}")).Single();
        var seenPlan = (await (await client.SendAsync(ben, HttpMethod.Get, $"/api/v1/plans/2026/10?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!;

        Assert.Equal(10m, own.Unpaid);
        Assert.Equal(UnpaidOf(ownPlan), own.Unpaid);
        Assert.Equal(890m, seen.Unpaid);
        Assert.Equal(UnpaidOf(seenPlan), seen.Unpaid);
        Assert.All(seen.Pots, pot => Assert.Equal(0m, pot.Booked));
    }

    [Fact]
    public async Task Existing_future_months_come_along_even_in_another_year()
    {
        var anna = await client.PersonAsync("Anna");
        foreach (var (year, month) in new[] { (2026, 1), (2026, 10), (2026, 11), (2027, 1) })
        {
            (await client.SendAsync(anna, HttpMethod.Get, $"/api/v1/plans/{year}/{month}")).EnsureSuccessStatusCode();
        }

        var months = await OverviewOfAsync(anna, "?year=2026");

        Assert.Equal([(2027, 1), (2026, 11), (2026, 10), (2026, 1)], months.Select(m => (m.Year, m.Month)));
    }

    [Fact]
    public async Task A10_the_overview_reads_only_and_creates_no_plan()
    {
        var anna = await client.PersonAsync("Anna");

        var months = await OverviewOfAsync(anna, "?year=2026");

        Assert.Empty(months);
        Assert.Empty(await OverviewOfAsync(anna, "?year=2026"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?year=1999")]
    [InlineData("?year=2101")]
    [InlineData("?year=abc")]
    public async Task A10_an_invalid_year_answers_bad_request(string query)
    {
        var anna = await client.PersonAsync("Anna");

        var response = await client.SendAsync(anna, HttpMethod.Get, Overview + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A10_a_second_person_gets_none_of_the_first_persons_months()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await PositionAsync(anna, 2026, 10, "Miete", 890m, PotKeys.Needs, "housing.rent");

        var bens = await OverviewOfAsync(ben, "?year=2026");
        var refused = await client.SendAsync(ben, HttpMethod.Get, $"{Overview}?year=2026&owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var granted = await OverviewOfAsync(ben, $"?year=2026&owner={anna.Id}");

        Assert.Empty(bens);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal([(2026, 10)], granted.Select(m => (m.Year, m.Month)));
    }

    [Fact]
    public async Task The_overview_needs_a_signed_in_person()
    {
        var response = await client.GetAsync($"{Overview}?year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
