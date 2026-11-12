using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// "Jetzt verfügbar" (Spec 139) über die API: Kontostände von heute der verfügbaren Konten minus die offenen Ausgaben des laufenden Monats, nur im
/// laufenden Monat, und im Haushalt nur mit den Mitgliedern, deren Konten und Buchungen der Betrachter sehen darf.
/// Die Uhr steht am 20. Oktober 2026.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AvailableNowTests : IDisposable
{
    private const string Plans = "/api/v1/plans";
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public AvailableNowTests(ApiFixture fixture)
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

    private async Task<Guid> PositionAsync(Person person, int month, string label, decimal amount, string pot, string category)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/{month}/positions",
            new CreatePositionRequest(label, amount, 25, CategoryCatalog.IdFor(category), pot));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await created.Content.ReadFromJsonAsync<PlanPositionResponse>())!.Id;
    }

    private async Task BookAsync(Person person, string kind, decimal amount, string pot, string category, Guid? position = null)
    {
        var booked = await client.SendAsync(person, HttpMethod.Post, "/api/v1/bookings", new SaveBookingRequest(
            kind, await GiroAsync(person), null, Today, amount, null,
            [new SaveBookingLine(amount, pot, CategoryCatalog.IdFor(category), position)], 2026, 10));
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
    }

    /// <summary>Konto mit 1.000 € Einnahme, Miete 800 offen, Strom 100 mit 40 gebucht, Gehalt 2.000 geplant.</summary>
    private async Task PlanOctoberAsync(Person person)
    {
        await BookAsync(person, BookingKinds.Income, 1000m, PotKeys.Income, "income.earned");
        await PositionAsync(person, 10, "Gehalt", 2000m, PotKeys.Income, "income.earned");
        await PositionAsync(person, 10, "Miete", 800m, PotKeys.Needs, "housing.rent");
        var power = await PositionAsync(person, 10, "Strom", 100m, PotKeys.Needs, "housing.utilities");
        await BookAsync(person, BookingKinds.Expense, 40m, PotKeys.Needs, "housing.utilities", power);
    }

    private async Task<T> GetAsync<T>(Person viewer, string path)
    {
        var answer = await client.SendAsync(viewer, HttpMethod.Get, path);
        Assert.True(answer.StatusCode == HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        return (await answer.Content.ReadFromJsonAsync<T>())!;
    }

    // A1: Kontostände minus offene Ausgaben; das geplante Gehalt zählt nicht.
    [Fact]
    public async Task A1_the_current_month_shows_balances_minus_open_expenses()
    {
        var person = await client.PersonAsync("Anna");
        await PlanOctoberAsync(person);

        var figure = (await GetAsync<PlanResponse>(person, $"{Plans}/2026/10")).Overview!.AvailableNow;

        Assert.NotNull(figure);
        Assert.Equal(960m, figure.Balances);
        Assert.Equal(860m, figure.OpenExpenses);
        Assert.Equal(100m, figure.Value);
    }

    // A3/A4: Verplanbar bleibt, wie es war; ein anderer Monat hat keine Zahl.
    [Fact]
    public async Task A4_another_month_has_no_available_now()
    {
        var person = await client.PersonAsync("Anna");
        await PositionAsync(person, 11, "Miete", 800m, PotKeys.Needs, "housing.rent");

        Assert.Null((await GetAsync<PlanResponse>(person, $"{Plans}/2026/11")).Overview!.AvailableNow);
    }

    // A5: Ohne Freigabe von Bens Konten und Buchungen fehlt er in der Summe, und der Hinweis nennt ihn.
    [Fact]
    public async Task A5_the_household_leaves_out_a_member_without_grant_and_names_them()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = (await client.MineAsync(anna)).Id;
        await PlanOctoberAsync(anna);
        await PlanOctoberAsync(ben);

        var figure = (await GetAsync<HouseholdPlanResponse>(anna, $"/api/v1/households/{household}/plans/2026/10")).AvailableNow;

        Assert.NotNull(figure);
        Assert.Equal(960m, figure.Balances);
        Assert.Equal(860m, figure.OpenExpenses);
        Assert.Equal(["Ben"], figure.Missing);
    }

    // R4 (Person-Wechsel): ohne Freigabe der Konten bekommt der Betrachter keine Zahl, mit ihr dieselbe wie die Person selbst.
    [Fact]
    public async Task R4_a_viewer_without_the_accounts_grant_gets_no_number_and_with_it_the_same()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await PlanOctoberAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));

        var without = await GetAsync<PlanResponse>(ben, $"{Plans}/2026/10?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View, Accounts: AccessLevelKeys.View));
        var with = await GetAsync<PlanResponse>(ben, $"{Plans}/2026/10?owner={anna.Id}");

        Assert.Null(without.Overview!.AvailableNow);
        Assert.Equal(100m, with.Overview!.AvailableNow!.Value);
    }
}
