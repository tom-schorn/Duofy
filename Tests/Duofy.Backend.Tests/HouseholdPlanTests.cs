using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Duofy.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Api.Tests;

/// <summary>
/// Der Haushaltsplan über die API mit echter Datenbank (Spec 69, A1 bis A9): zusammengesetzt aus den Monaten der
/// gezählten Mitglieder, mit Besitzernamen, Zahlen, nicht zugeordneten Buchungen, Konten nur mit Freigabe und nur
/// zum Lesen. "Heute" ist hier der 20. November 2026.
/// </summary>
[Collection(ApiCollection.Name)]
public class HouseholdPlanTests : IDisposable
{
    private static readonly Guid Rent = CategoryCatalog.IdFor("housing.rent");
    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");
    private static readonly Guid Salary = CategoryCatalog.IdFor("income.earned");
    private static readonly Guid Gifts = CategoryCatalog.IdFor("personal.gifts");

    private readonly ApiFixture fixture;
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public HouseholdPlanTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        var today = new FixedTime(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Eine Person des Haushalts mit ihrem Girokonto.</summary>
    private sealed record Member(Person Person, Guid Giro);

    private async Task<Member> WithGiroAsync(Person person)
    {
        var response = await client.SendAsync(person, HttpMethod.Post, "/api/v1/accounts",
            new CreateAccountRequest("Giro " + person.FirstName, AccountTypes.Checking, 100m, new DateOnly(2026, 1, 1)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new Member(person, (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id);
    }

    private async Task<(HouseholdResponse Household, Member Anna, Member Ben)> AnnaAndBenWithGiroAsync()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        return (await client.MineAsync(anna), await WithGiroAsync(anna), await WithGiroAsync(ben));
    }

    private static string PlanPath(Guid householdId, int year = 2026, int month = 11) =>
        $"/api/v1/households/{householdId}/plans/{year}/{month}";

    private async Task<HouseholdPlanResponse> HouseholdPlanAsync(Person viewer, Guid householdId, int year = 2026, int month = 11)
    {
        var response = await client.SendAsync(viewer, HttpMethod.Get, PlanPath(householdId, year, month));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<HouseholdPlanResponse>())!;
    }

    private async Task<PlanPositionResponse> PositionAsync(
        Person owner, string label, decimal amount, Guid category, string pot = PotKeys.Needs, int month = 11)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, $"/api/v1/plans/2026/{month}/positions",
            new CreatePositionRequest(label, amount, 10, category, pot));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
    }

    private async Task BookAsync(Member member, decimal amount, string pot, Guid category, Guid? position = null, int month = 11)
    {
        var kind = pot == PotKeys.Income ? BookingKinds.Income : BookingKinds.Expense;
        var response = await client.SendAsync(member.Person, HttpMethod.Post, "/api/v1/bookings",
            new SaveBookingRequest(kind, member.Giro, null, new DateOnly(2026, month, 5), amount, null,
                [new SaveBookingLine(amount, pot, category, position)]));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private Task<int> PlansOfAsync(Guid ownerId, int month = 11) =>
        fixture.Factory.WithDbAsync(db => db.Plans.CountAsync(p => p.OwnerId == ownerId && p.Year == 2026 && p.Month == month));

    /// <summary>Setzt das Beitrittsdatum von Hand, damit der Test bestimmt, ab welchem Monat jemand zählt.</summary>
    private Task JoinedOnAsync(Guid? userId, DateTimeOffset joinedAt) =>
        fixture.Factory.WithDbAsync(db => db.HouseholdMembers
            .Where(m => userId == null || m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.JoinedAt, joinedAt)));

    // A1
    [Fact]
    public async Task Every_member_sees_every_position_with_its_owner_although_nothing_is_granted()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var rent = await PositionAsync(anna.Person, "Miete", 890.00m, Rent);
        var power = await PositionAsync(ben.Person, "Strom", 80.00m, Groceries);
        await BookAsync(ben, 30.00m, PotKeys.Needs, Groceries, power.Id);

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        var annasRent = plan.Positions.Single(p => p.Label == "Miete");
        Assert.Equal((rent.Id, anna.Person.Id, "Anna", 890.00m, 0m), (annasRent.Id, annasRent.OwnerId, annasRent.OwnerFirstName, annasRent.AmountPlanned, annasRent.Booked));
        var bensPower = plan.Positions.Single(p => p.Label == "Strom");
        Assert.Equal((ben.Person.Id, "Ben", 80.00m, 30.00m), (bensPower.OwnerId, bensPower.OwnerFirstName, bensPower.AmountPlanned, bensPower.Booked));
        Assert.Null(bensPower.DoneAt);
        Assert.Equal(household.Name, plan.HouseholdName);
        Assert.Equal((2026, 11), (plan.Year, plan.Month));
    }

    // R3: nach Topf, Fälligkeitstag und Name
    [Fact]
    public async Task Positions_are_sorted_by_pot_then_due_day_then_name()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await PositionAsync(anna.Person, "Zeitung", 10.00m, Gifts, PotKeys.Wants);
        await PositionAsync(ben.Person, "Brot", 5.00m, Groceries);
        await PositionAsync(anna.Person, "Apfel", 5.00m, Groceries);
        await PositionAsync(anna.Person, "Gehalt", 3000.00m, Salary, PotKeys.Income);

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        Assert.Equal(["Gehalt", "Apfel", "Brot", "Zeitung"], plan.Positions.Select(p => p.Label));
    }

    private async Task ContractAsync(Person owner, string label, decimal amount, Guid category) =>
        (await client.SendAsync(owner, HttpMethod.Post, "/api/v1/commitments",
            new CreateCommitmentRequest(CommitmentTypes.Contract, label, amount, 1, new DateOnly(2026, 1, 1), null, category, PotKeys.Needs,
                PaymentMethod: PaymentMethods.Other))).EnsureSuccessStatusCode();

    // A2, A11: Bens fehlender Monat entsteht einmal und nur aus Bens Verträgen, nicht aus Annas oder denen
    // einer Fremden.
    [Fact]
    public async Task Opening_creates_the_missing_months_of_the_members_once()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var clara = await client.FounderAsync("Clara");
        await ContractAsync(ben.Person, "Miete", 890.00m, Rent);
        await ContractAsync(anna.Person, "Zeitung Anna", 15.00m, Gifts);
        await ContractAsync(clara, "Handy Clara", 20.00m, Groceries);
        (await client.SendAsync(anna.Person, HttpMethod.Get, "/api/v1/plans/2026/11")).EnsureSuccessStatusCode();
        Assert.Equal(0, await PlansOfAsync(ben.Person.Id));

        var first = await HouseholdPlanAsync(anna.Person, household.Id);
        var second = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal(1, await PlansOfAsync(ben.Person.Id));
        var rent = Assert.Single(first.Positions, p => p.OwnerId == ben.Person.Id);
        Assert.Equal("Miete", rent.Label);
        Assert.Equal(rent.Id, Assert.Single(second.Positions, p => p.OwnerId == ben.Person.Id).Id);
        var bensMonth = await fixture.Factory.WithDbAsync(db => db.Plans
            .Where(p => p.OwnerId == ben.Person.Id && p.Year == 2026 && p.Month == 11)
            .SelectMany(p => p.Positions.Select(x => x.Label))
            .ToListAsync());
        Assert.Equal(["Miete"], bensMonth);
    }

    // A3
    [Fact]
    public async Task Income_and_quotas_count_over_all_members_without_pass_through()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        // Der Vertrag steht vor dem ersten Öffnen des Monats, sonst käme er erst als Unterschied zum Übernehmen.
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/commitments",
            new CreateCommitmentRequest(CommitmentTypes.Contract, "Weiterleitung", 200.00m, 1, new DateOnly(2026, 1, 1), null, Groceries, PotKeys.Needs,
                PassThrough: true, PaymentMethod: PaymentMethods.Other))).EnsureSuccessStatusCode();
        var annasSalary = await PositionAsync(anna.Person, "Gehalt Anna", 3000.00m, Salary, PotKeys.Income);
        var bensSalary = await PositionAsync(ben.Person, "Gehalt Ben", 2000.00m, Salary, PotKeys.Income);
        var rent = await PositionAsync(anna.Person, "Miete", 1500.00m, Rent);
        var power = await PositionAsync(ben.Person, "Strom", 1000.00m, Groceries);
        var forwarding = (await HouseholdPlanAsync(anna.Person, household.Id)).Positions.Single(p => p.Label == "Weiterleitung");
        await BookAsync(anna, 3000.00m, PotKeys.Income, Salary, annasSalary.Id);
        await BookAsync(ben, 2000.00m, PotKeys.Income, Salary, bensSalary.Id);
        await BookAsync(anna, 1500.00m, PotKeys.Needs, Rent, rent.Id);
        await BookAsync(ben, 1000.00m, PotKeys.Needs, Groceries, power.Id);
        await BookAsync(anna, 200.00m, PotKeys.Needs, Groceries, forwarding.Id);
        (await client.SendAsync(anna.Person, HttpMethod.Patch, $"/api/v1/households/{household.Id}",
            new UpdateHouseholdRequest(QuotaNeeds: 50, QuotaWants: 30, QuotaSavings: 20))).EnsureSuccessStatusCode();

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        Assert.Equal(5000.00m, plan.Income);
        var needs = plan.Quotas.Single(q => q.Pot == PotKeys.Needs);
        Assert.Equal((50, 50.0m), (needs.Target, needs.Actual));
        Assert.Equal(2500.00m, plan.Pots.Single(p => p.Pot == PotKeys.Needs).Planned);
    }

    // A4
    [Fact]
    public async Task Bookings_without_a_position_are_unplanned_by_pot_but_transfers_and_the_carry_over_are_not()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(ben, 40.00m, PotKeys.Wants, Gifts);
        var savings = await WithGiroAsync(ben.Person);
        (await client.SendAsync(ben.Person, HttpMethod.Post, "/api/v1/bookings",
            new SaveBookingRequest(BookingKinds.Transfer, ben.Giro, savings.Giro, new DateOnly(2026, 11, 6), 100.00m, null, null)))
            .EnsureSuccessStatusCode();
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(500.00m)))
            .EnsureSuccessStatusCode();
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/12/carry-over", new CreateCarryOverRequest(300.00m)))
            .EnsureSuccessStatusCode();

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal(new HouseholdUnplannedResponse(0m, 0m, 40.00m, 0m), plan.Unplanned);
    }

    // A13: Ein negativer Rest bucht die Einnahme "Übertrag nach November" in den Oktober; sie ist kein verdientes
    // Geld und zählt im Haushaltsplan des Oktobers weder bei den Einnahmen der Quoten noch bei den nicht
    // zugeordneten Einnahmen.
    [Fact]
    public async Task A_negative_carry_over_into_the_next_month_counts_nowhere_in_the_household_plan()
    {
        var (household, anna, _) = await AnnaAndBenWithGiroAsync();
        await JoinedOnAsync(null, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var salary = await PositionAsync(anna.Person, "Gehalt", 1000.00m, Salary, PotKeys.Income, month: 10);
        var rent = await PositionAsync(anna.Person, "Miete", 500.00m, Rent, month: 10);
        await BookAsync(anna, 1000.00m, PotKeys.Income, Salary, salary.Id, month: 10);
        await BookAsync(anna, 500.00m, PotKeys.Needs, Rent, rent.Id, month: 10);
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(-250.00m)))
            .EnsureSuccessStatusCode();

        var october = await HouseholdPlanAsync(anna.Person, household.Id, 2026, 10);

        Assert.Equal(50.0m, october.Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
        Assert.Equal(0m, october.Unplanned.Income);
    }

    // A5
    [Fact]
    public async Task Whoever_left_disappears_from_the_plan_and_whoever_joined_counts_from_their_month_on()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var clara = await WithGiroAsync(await client.PersonAsync("Clara"));
        await client.JoinAsync(anna.Person, clara.Person);
        await JoinedOnAsync(null, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await JoinedOnAsync(clara.Person.Id, new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        await PositionAsync(clara.Person, "Handy", 20.00m, Groceries);
        await PositionAsync(ben.Person, "Strom", 80.00m, Groceries);
        await PositionAsync(anna.Person, "Miete", 890.00m, Rent, month: 10);

        var october = await HouseholdPlanAsync(anna.Person, household.Id, 2026, 10);
        var november = await HouseholdPlanAsync(anna.Person, household.Id);
        (await client.SendAsync(ben.Person, HttpMethod.Delete, $"/api/v1/households/{household.Id}/members/me")).EnsureSuccessStatusCode();
        var afterLeaving = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal("Miete", Assert.Single(october.Positions).Label);
        Assert.Equal(0, await PlansOfAsync(clara.Person.Id, 10));
        Assert.Contains(november.Positions, p => p.OwnerId == clara.Person.Id);
        Assert.Contains(november.Positions, p => p.OwnerId == ben.Person.Id);
        Assert.DoesNotContain(afterLeaving.Positions, p => p.OwnerId == ben.Person.Id);
    }

    // A5: Das Datum des Beitritts zählt in Berlin; 00:30 am 1. November ist in UTC noch der 31. Oktober.
    [Fact]
    public async Task Joining_after_midnight_in_berlin_counts_from_that_day_on_although_utc_is_still_the_day_before()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await JoinedOnAsync(null, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await JoinedOnAsync(ben.Person.Id, new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero));
        await PositionAsync(ben.Person, "Strom", 80.00m, Groceries, month: 10);
        await PositionAsync(ben.Person, "Strom", 80.00m, Groceries);

        var october = await HouseholdPlanAsync(anna.Person, household.Id, 2026, 10);
        var november = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.DoesNotContain(october.Positions, p => p.OwnerId == ben.Person.Id);
        Assert.Contains(november.Positions, p => p.OwnerId == ben.Person.Id);
    }

    // A6
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task The_household_plan_has_no_write_path(string method)
    {
        var (household, anna, _) = await AnnaAndBenWithGiroAsync();
        await PositionAsync(anna.Person, "Miete", 890.00m, Rent);

        var month = await client.SendAsync(anna.Person, new HttpMethod(method), PlanPath(household.Id), new { });
        var list = await client.SendAsync(anna.Person, new HttpMethod(method), $"/api/v1/households/{household.Id}/plans", new { });

        Assert.Contains(month.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        Assert.Contains(list.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        Assert.Equal("Miete", Assert.Single((await HouseholdPlanAsync(anna.Person, household.Id)).Positions).Label);
    }

    // A7
    [Fact]
    public async Task The_months_are_listed_newest_first_for_the_months_with_a_plan_of_a_counted_member()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await JoinedOnAsync(null, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        (await client.SendAsync(anna.Person, HttpMethod.Get, "/api/v1/plans/2026/10")).EnsureSuccessStatusCode();
        (await client.SendAsync(anna.Person, HttpMethod.Get, "/api/v1/plans/2026/11")).EnsureSuccessStatusCode();
        (await client.SendAsync(ben.Person, HttpMethod.Get, "/api/v1/plans/2026/11")).EnsureSuccessStatusCode();

        var response = await client.SendAsync(ben.Person, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([new HouseholdPlanMonth(2026, 11), new HouseholdPlanMonth(2026, 10)],
            await response.Content.ReadFromJsonAsync<List<HouseholdPlanMonth>>());
    }

    // A8
    [Fact]
    public async Task Accounts_show_only_for_members_who_granted_them_and_the_others_are_named()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var clara = await WithGiroAsync(await client.PersonAsync("Clara"));
        await client.JoinAsync(anna.Person, clara.Person);
        await client.GrantAsync(anna.Person, ben.Person, new UpdateGrantsRequest(Accounts: AccessLevelKeys.View));

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        Assert.Equal(new[] { anna.Giro, ben.Giro }.Order(), plan.Accounts.Select(a => a.AccountId).Order());
        Assert.Equal("Anna", plan.Accounts.Single(a => a.AccountId == anna.Giro).OwnerFirstName);
        Assert.Equal(["Clara"], plan.AccountsMissing);
    }

    // A9
    [Fact]
    public async Task Someone_outside_the_household_gets_household_not_found()
    {
        var (household, _, _) = await AnnaAndBenWithGiroAsync();
        var clara = await client.FounderAsync("Clara");
        var loner = await client.PersonAsync("Dora");

        var month = await client.SendAsync(clara, HttpMethod.Get, PlanPath(household.Id));
        var list = await client.SendAsync(clara, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans");
        var withoutHousehold = await client.SendAsync(loner, HttpMethod.Get, PlanPath(household.Id));

        foreach (var response in new[] { month, list, withoutHousehold })
        {
            Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.HouseholdNotFound), (response.StatusCode, await response.ReadCodeAsync()));
        }
    }

    [Fact]
    public async Task A_month_outside_the_range_is_refused_and_a_stranger_cannot_probe_for_it()
    {
        var (household, anna, _) = await AnnaAndBenWithGiroAsync();
        var clara = await client.FounderAsync("Clara");

        var tooEarly = await client.SendAsync(anna.Person, HttpMethod.Get, PlanPath(household.Id, 1999, 12));
        var thirteen = await client.SendAsync(anna.Person, HttpMethod.Get, PlanPath(household.Id, 2026, 13));
        var stranger = await client.SendAsync(clara, HttpMethod.Get, PlanPath(household.Id, 1999, 12));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.MonthInvalid), (tooEarly.StatusCode, await tooEarly.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, thirteen.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task The_household_plan_needs_a_signed_in_person()
    {
        var response = await client.GetAsync(PlanPath(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
