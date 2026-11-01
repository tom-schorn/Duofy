using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Monatsliste der Plan-Übersicht über die API mit echter Datenbank (Spec 23, A2, A4, A7): die Monate mit einem
/// Plan, neueste zuerst, jeder mit seinem Frei-Betrag; für eine Person, mit Freigabe für ein Mitglied und für den
/// Haushalt. Die Liste legt nichts an.
/// </summary>
[Collection(ApiCollection.Name)]
public class PlanOverviewTests
{
    private const string Months = "/api/v1/plans";

    private static readonly Guid Rent = CategoryCatalog.IdFor("housing.rent");
    private static readonly Guid Salary = CategoryCatalog.IdFor("income.earned");

    private readonly ApiFixture fixture;
    private readonly HttpClient client;

    public PlanOverviewTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        client = fixture.Factory.CreateClient();
    }

    private async Task PositionAsync(Person owner, int month, string label, decimal amount, string pot, Guid category, HttpClient? via = null)
    {
        var response = await (via ?? client).SendAsync(owner, HttpMethod.Post, $"/api/v1/plans/2026/{month}/positions",
            new CreatePositionRequest(label, amount, 10, category, pot));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private async Task<List<PlanMonthResponse>> MonthsOfAsync(Person viewer, string query = "")
    {
        var response = await client.SendAsync(viewer, HttpMethod.Get, Months + query);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<PlanMonthResponse>>())!;
    }

    // A2: die Monate mit Plan, neueste zuerst
    [Fact]
    public async Task The_months_with_a_plan_are_listed_newest_first()
    {
        var anna = await client.PersonAsync("Anna");
        (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/9")).EnsureSuccessStatusCode();
        (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/10")).EnsureSuccessStatusCode();
        (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2025/12")).EnsureSuccessStatusCode();

        var months = await MonthsOfAsync(anna);

        Assert.Equal([(2026, 10), (2026, 9), (2025, 12)], months.Select(m => (m.Year, m.Month)));
    }

    // A4: der Frei-Betrag aus den geplanten Zahlen des Monats
    [Fact]
    public async Task Each_month_carries_its_free_amount_from_the_planned_figures()
    {
        var anna = await client.PersonAsync("Anna");
        await PositionAsync(anna, 10, "Gehalt", 3000m, PotKeys.Income, Salary);
        await PositionAsync(anna, 10, "Miete", 890m, PotKeys.Needs, Rent);
        await PositionAsync(anna, 9, "Miete", 890m, PotKeys.Needs, Rent);

        var months = await MonthsOfAsync(anna);

        Assert.Equal(2110m, months.Single(m => m.Month == 10).Free);
        Assert.Equal(-890m, months.Single(m => m.Month == 9).Free);
        var october = await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/10");
        var plan = (await october.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Equal(plan.PlannedIncome - plan.PlannedExpenses, months.Single(m => m.Month == 10).Free);
    }

    [Fact]
    public async Task Listing_the_months_creates_no_plan_and_shows_only_the_own_months()
    {
        var anna = await client.PersonAsync("Anna");
        var ben = await client.PersonAsync("Ben");
        (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/10")).EnsureSuccessStatusCode();
        var before = await fixture.Factory.WithDbAsync(db => db.Plans.CountAsync(p => p.OwnerId == ben.Id));

        var bens = await MonthsOfAsync(ben);
        var annas = await MonthsOfAsync(anna);

        Assert.Empty(bens);
        Assert.Equal([(2026, 10)], annas.Select(m => (m.Year, m.Month)));
        Assert.Equal(before, await fixture.Factory.WithDbAsync(db => db.Plans.CountAsync(p => p.OwnerId == ben.Id)));
    }

    [Fact]
    public async Task With_plan_view_ben_lists_annas_months_and_without_a_grant_gets_not_found()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        (await client.SendAsync(anna, HttpMethod.Get, "/api/v1/plans/2026/10")).EnsureSuccessStatusCode();

        var refused = await client.SendAsync(ben, HttpMethod.Get, $"{Months}?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));
        var granted = await MonthsOfAsync(ben, $"?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal([(2026, 10)], granted.Select(m => (m.Year, m.Month)));
    }

    [Fact]
    public async Task The_month_list_needs_a_signed_in_person()
    {
        var response = await client.GetAsync(Months);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A4 für den Haushalt: Frei aus den Plänen der gezählten Mitglieder
    [Fact]
    public async Task The_household_list_carries_the_free_amount_of_all_counted_members()
    {
        // Feste Uhr im Oktober: Ein Mitglied zählt erst ab dem Monat seines Beitritts; mit der echten Uhr wäre der Test
        // ab November rot.
        var today = new MovableTime(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
        await using var api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
        var inOctober = api.CreateClient();
        var (anna, ben) = await inOctober.AnnaAndBenAsync();
        var household = await inOctober.MineAsync(anna);
        await PositionAsync(anna, 10, "Gehalt", 3000m, PotKeys.Income, Salary, inOctober);
        await PositionAsync(anna, 10, "Miete", 890m, PotKeys.Needs, Rent, inOctober);
        await PositionAsync(ben, 10, "Gehalt", 1000m, PotKeys.Income, Salary, inOctober);

        var response = await inOctober.SendAsync(ben, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var october = (await response.Content.ReadFromJsonAsync<List<HouseholdPlanMonth>>())!.Single(m => (m.Year, m.Month) == (2026, 10));
        Assert.Equal(3110m, october.Free);
    }

    // A7
    [Fact]
    public async Task Someone_from_another_household_gets_not_found_for_the_household_list()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var household = await client.MineAsync(anna);
        var clara = await client.FounderAsync("Clara");

        var response = await client.SendAsync(clara, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.HouseholdNotFound), (response.StatusCode, await response.ReadCodeAsync()));
    }
}
