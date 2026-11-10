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
        var (anna, ben) = await client.AnnaAndBenAsync(fixture.Factory);
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

    // Spec 24, A13: der Haushalt zeigt Festes, Frei und die Buchungen beider Mitglieder, neueste zuerst.
    [Fact]
    public async Task The_household_plan_carries_fixed_free_and_the_bookings_of_all_members()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await PositionAsync(anna.Person, "Gehalt", 3000.00m, Salary, PotKeys.Income);
        var rent = await PositionAsync(anna.Person, "Miete", 1000.00m, Rent);
        var power = await PositionAsync(ben.Person, "Strom", 200.00m, Groceries);
        await BookAsync(anna, 1000.00m, PotKeys.Needs, Rent, rent.Id);
        await BookAsync(ben, 60.00m, PotKeys.Needs, Groceries, power.Id);

        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal(1200.00m, plan.Fixed);
        Assert.Equal(1800.00m, plan.Free);
        Assert.Empty(plan.BookingsMissing!);
        Assert.Equal(2, plan.Bookings!.Count);
        Assert.Equal(["Anna", "Ben"], plan.Bookings.Select(b => b.OwnerFirstName).Order());
        Assert.Equal(plan.Bookings.OrderByDescending(b => b.Date).Select(b => b.Id), plan.Bookings.Select(b => b.Id));
    }

    // Einzelne Buchungen folgen den Freigaben wie die Konten; wer nichts freigibt, steht mit Namen als fehlend da.
    [Fact]
    public async Task Single_bookings_show_only_for_members_who_granted_them_and_the_others_are_named()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(anna, 20.00m, PotKeys.Needs, Groceries);
        await BookAsync(ben, 30.00m, PotKeys.Needs, Groceries);

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal([anna.Person.Id], plan.Bookings!.Select(b => b.OwnerId));
        Assert.Equal(["Ben"], plan.BookingsMissing);
    }

    // Spec 46, A2: die Freigabe gilt je Richtung; die eigenen Buchungen sieht man immer.
    [Fact]
    public async Task The_booking_grant_counts_in_one_direction_and_the_own_bookings_always_show()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(anna, 20.00m, PotKeys.Needs, Groceries);
        await BookAsync(ben, 30.00m, PotKeys.Needs, Groceries);
        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        var seenByAnna = await HouseholdPlanAsync(anna.Person, household.Id);
        var seenByBen = await HouseholdPlanAsync(ben.Person, household.Id);

        Assert.Equal(2, seenByAnna.Bookings!.Count);
        Assert.Empty(seenByAnna.BookingsMissing!);
        Assert.Equal([ben.Person.Id], seenByBen.Bookings!.Select(b => b.OwnerId));
        Assert.Equal(["Anna"], seenByBen.BookingsMissing);
    }

    // Spec 46, A3: nimmt Ben die Freigabe zurück, verschwinden seine Buchungen aus der nächsten Antwort.
    [Fact]
    public async Task Taking_the_booking_grant_back_hides_the_bookings_again()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(ben, 30.00m, PotKeys.Needs, Groceries);
        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.View));
        Assert.Single((await HouseholdPlanAsync(anna.Person, household.Id)).Bookings!);

        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.None));
        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Empty(plan.Bookings!);
        Assert.Equal(["Ben"], plan.BookingsMissing);
    }

    // Spec 46, A4: nur der Bereich Buchungen öffnet die Buchungen, nicht Konten oder Plan.
    [Fact]
    public async Task Grants_of_other_areas_do_not_show_the_bookings()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(ben, 30.00m, PotKeys.Needs, Groceries);
        await client.GrantAsync(ben.Person, anna.Person,
            new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete, Accounts: AccessLevelKeys.Delete, Commitments: AccessLevelKeys.Delete));

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Empty(plan.Bookings!);
        Assert.Equal(["Ben"], plan.BookingsMissing);
    }

    // Spec 46, A5 und A10: nichts Weiteres in den Antworten des Haushalts trägt die Kennung einer gesperrten Buchung.
    [Fact]
    public async Task No_answer_of_the_household_carries_the_id_of_a_hidden_booking()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var power = await PositionAsync(ben.Person, "Strom", 80.00m, Groceries);
        await BookAsync(ben, 12.34m, PotKeys.Needs, Groceries, power.Id);
        var hiddenId = (await HouseholdPlanAsync(ben.Person, household.Id)).Bookings!.Single().Id.ToString();

        var plan = await client.SendAsync(anna.Person, HttpMethod.Get, PlanPath(household.Id));
        var months = await client.SendAsync(anna.Person, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans");
        var mine = await client.SendAsync(anna.Person, HttpMethod.Get, "/api/v1/households/mine");

        foreach (var response in new[] { plan, months, mine })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(hiddenId, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Spec 46, A6: Summen, Quoten und Soll-Ist bleiben gleich, ob die Buchungen freigegeben sind oder nicht.
    [Fact]
    public async Task The_figures_are_the_same_with_and_without_the_booking_grant()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await PositionAsync(anna.Person, "Gehalt", 3000.00m, Salary, PotKeys.Income);
        var power = await PositionAsync(ben.Person, "Strom", 80.00m, Groceries);
        await BookAsync(ben, 60.00m, PotKeys.Needs, Groceries, power.Id);
        await BookAsync(ben, 40.00m, PotKeys.Wants, Gifts);

        var without = await HouseholdPlanAsync(anna.Person, household.Id);
        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.View));
        var with = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Empty(without.Bookings!);
        Assert.Single(with.Bookings!, b => b.Amount == 60.00m);
        Assert.Equal(with.Income, without.Income);
        Assert.Equal(with.Fixed, without.Fixed);
        Assert.Equal(with.Free, without.Free);
        Assert.Equal(with.Unpaid, without.Unpaid);
        Assert.Equal(with.Pots, without.Pots);
        Assert.Equal(with.Quotas, without.Quotas);
        Assert.Equal(with.Unplanned, without.Unplanned);
        Assert.Equal(with.Positions.Select(p => (p.Id, p.Booked)), without.Positions.Select(p => (p.Id, p.Booked)));
    }

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

    private async Task SetQuotasAsync(Person founder, Guid householdId) =>
        (await client.SendAsync(founder, HttpMethod.Patch, $"/api/v1/households/{householdId}",
            new UpdateHouseholdRequest(QuotaNeeds: 50, QuotaWants: 30, QuotaSavings: 20))).EnsureSuccessStatusCode();

    /// <summary>Anna mit Einnahmen 3000 und Grundbedarf 1800, Ben mit 2000 und 700, Quoten 50/30/20 (Spec 27, A1).</summary>
    private async Task<(HouseholdResponse Household, Member Anna, Member Ben)> AnnaAndBenWithFiguresAsync()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var annasSalary = await PositionAsync(anna.Person, "Gehalt Anna", 3000.00m, Salary, PotKeys.Income);
        var bensSalary = await PositionAsync(ben.Person, "Gehalt Ben", 2000.00m, Salary, PotKeys.Income);
        var rent = await PositionAsync(anna.Person, "Miete", 1800.00m, Rent);
        var power = await PositionAsync(ben.Person, "Strom", 700.00m, Groceries);
        await BookAsync(anna, 3000.00m, PotKeys.Income, Salary, annasSalary.Id);
        await BookAsync(ben, 2000.00m, PotKeys.Income, Salary, bensSalary.Id);
        await BookAsync(anna, 1800.00m, PotKeys.Needs, Rent, rent.Id);
        await BookAsync(ben, 700.00m, PotKeys.Needs, Groceries, power.Id);
        await SetQuotasAsync(anna.Person, household.Id);
        return (household, anna, ben);
    }

    // Spec 27, A1 und A5: je Person die Quote, der Haushalt unverändert
    [Fact]
    public async Task Each_counted_member_gets_their_own_quota_next_to_the_unchanged_household_figures()
    {
        var (household, anna, ben) = await AnnaAndBenWithFiguresAsync();

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal(50.0m, plan.Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
        Assert.Equal(5000.00m, plan.Income);
        Assert.Equal(2500.00m, plan.Pots.Single(p => p.Pot == PotKeys.Needs).Booked);
        var members = plan.Members.ToDictionary(m => m.UserId);
        Assert.Equal(2, members.Count);
        Assert.Equal(("Anna", 3000.00m, true), (members[anna.Person.Id].FirstName, members[anna.Person.Id].Income, members[anna.Person.Id].HasIncome));
        Assert.Equal(("Ben", 2000.00m, true), (members[ben.Person.Id].FirstName, members[ben.Person.Id].Income, members[ben.Person.Id].HasIncome));
        Assert.Equal(60.0m, members[anna.Person.Id].Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
        Assert.Equal(35.0m, members[ben.Person.Id].Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
        Assert.Equal(new[] { PotKeys.Needs, PotKeys.Wants, PotKeys.Savings }, members[anna.Person.Id].Quotas.Select(q => q.Pot));
    }

    // Spec 27, A2: Ziel ist der Haushaltswert, Abweichung Ist minus Ziel
    [Fact]
    public async Task The_deviation_is_the_actual_minus_the_household_target_in_percentage_points()
    {
        var (household, anna, ben) = await AnnaAndBenWithFiguresAsync();

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        var annasNeeds = plan.Members.Single(m => m.UserId == anna.Person.Id).Quotas.Single(q => q.Pot == PotKeys.Needs);
        var bensNeeds = plan.Members.Single(m => m.UserId == ben.Person.Id).Quotas.Single(q => q.Pot == PotKeys.Needs);
        Assert.Equal((50, 60.0m, 10.0m), (annasNeeds.Target, annasNeeds.Actual, annasNeeds.Deviation));
        Assert.Equal((50, 35.0m, -15.0m), (bensNeeds.Target, bensNeeds.Actual, bensNeeds.Deviation));
        var bensWants = plan.Members.Single(m => m.UserId == ben.Person.Id).Quotas.Single(q => q.Pot == PotKeys.Wants);
        Assert.Equal((30, 0.0m, -30.0m), (bensWants.Target, bensWants.Actual, bensWants.Deviation));
    }

    // Spec 27, A3: durchlaufende Posten und der Übertrag zählen bei einer Person nirgends
    [Fact]
    public async Task Pass_through_and_the_carry_over_count_for_no_member()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/commitments",
            new CreateCommitmentRequest(CommitmentTypes.Contract, "Weiterleitung", 200.00m, 1, new DateOnly(2026, 1, 1), null, Groceries, PotKeys.Needs,
                PassThrough: true, PaymentMethod: PaymentMethods.Other))).EnsureSuccessStatusCode();
        var salary = await PositionAsync(anna.Person, "Gehalt Anna", 3000.00m, Salary, PotKeys.Income);
        var rent = await PositionAsync(anna.Person, "Miete", 1800.00m, Rent);
        var forwarding = (await HouseholdPlanAsync(anna.Person, household.Id)).Positions.Single(p => p.Label == "Weiterleitung");
        await BookAsync(anna, 3000.00m, PotKeys.Income, Salary, salary.Id);
        await BookAsync(anna, 1800.00m, PotKeys.Needs, Rent, rent.Id);
        await BookAsync(anna, 200.00m, PotKeys.Needs, Groceries, forwarding.Id);
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(500.00m)))
            .EnsureSuccessStatusCode();
        await SetQuotasAsync(anna.Person, household.Id);

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        var annas = plan.Members.Single(m => m.UserId == anna.Person.Id);
        Assert.Equal(3000.00m, annas.Income);
        Assert.Equal(60.0m, annas.Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
    }

    // Spec 27, A4: ohne Einnahmen ist das Ist 0, hasIncome false und es gibt keinen Fehler
    [Fact]
    public async Task A_member_without_income_has_actual_zero_and_is_flagged()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var salary = await PositionAsync(anna.Person, "Gehalt Anna", 3000.00m, Salary, PotKeys.Income);
        var power = await PositionAsync(ben.Person, "Strom", 700.00m, Groceries);
        await BookAsync(anna, 3000.00m, PotKeys.Income, Salary, salary.Id);
        await BookAsync(ben, 700.00m, PotKeys.Needs, Groceries, power.Id);
        await SetQuotasAsync(anna.Person, household.Id);

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        var bens = plan.Members.Single(m => m.UserId == ben.Person.Id);
        Assert.Equal((0m, false), (bens.Income, bens.HasIncome));
        Assert.All(bens.Quotas, q => Assert.Equal(0m, q.Actual));
        Assert.Equal(-50.0m, bens.Quotas.Single(q => q.Pot == PotKeys.Needs).Deviation);
    }

    // Spec 27, A7: auch ohne Freigabe stehen die Zahlen beider in members
    [Fact]
    public async Task Both_members_are_listed_for_the_viewer_although_nothing_is_granted()
    {
        var (household, anna, ben) = await AnnaAndBenWithFiguresAsync();

        var plan = await HouseholdPlanAsync(ben.Person, household.Id);

        Assert.Equal(new[] { anna.Person.Id, ben.Person.Id }.Order(),plan.Members.Select(m => m.UserId).Order());
        Assert.Equal(60.0m, plan.Members.Single(m => m.UserId == anna.Person.Id).Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
    }

    // A4
    [Fact]
    public async Task Bookings_without_a_position_are_unplanned_by_pot_but_transfers_and_the_carry_over_are_not()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await BookAsync(ben, 40.00m, PotKeys.Wants, Gifts);
        var savings = await WithGiroAsync(ben.Person);
        (await client.SendAsync(ben.Person, HttpMethod.Post, "/api/v1/bookings",
            new SaveBookingRequest(BookingKinds.Transfer, ben.Giro, savings.Giro, new DateOnly(2026, 11, 6), 100.00m, null,
                [new SaveBookingLine(100.00m, PotKeys.Savings, null)], TargetPot: PotKeys.Wants)))
            .EnsureSuccessStatusCode();
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/11/carry-over", new CreateCarryOverRequest(500.00m)))
            .EnsureSuccessStatusCode();
        (await client.SendAsync(anna.Person, HttpMethod.Post, "/api/v1/plans/2026/12/carry-over", new CreateCarryOverRequest(300.00m)))
            .EnsureSuccessStatusCode();

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        Assert.Equal(new HouseholdUnplannedResponse(0m, 0m, 40.00m, 0m), plan.Unplanned);
    }

    // Spec 112, A5 (R4): Der Haushaltsmonat zählt die Verschiebungen der Umbuchungen zwischen Töpfen aller Mitglieder, deren Buchungen zu sehen sind.
    [Fact]
    public async Task The_household_month_adds_the_pot_shifts_of_the_transfers_of_its_members()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        await client.GrantAsync(ben.Person, anna.Person, new UpdateGrantsRequest(Book: AccessLevelKeys.View));
        await BookAsync(anna, 2000.00m, PotKeys.Income, Salary);
        var before = await HouseholdPlanAsync(anna.Person, household.Id);
        foreach (var member in new[] { anna, ben })
        {
            var target = await WithGiroAsync(member.Person);
            (await client.SendAsync(member.Person, HttpMethod.Post, "/api/v1/bookings",
                new SaveBookingRequest(BookingKinds.Transfer, member.Giro, target.Giro, new DateOnly(2026, 11, 6), 100.00m, null,
                    [new SaveBookingLine(100.00m, PotKeys.Wants, null)], TargetPot: PotKeys.Savings)))
                .EnsureSuccessStatusCode();
        }

        var after = await HouseholdPlanAsync(anna.Person, household.Id);

        decimal Booked(HouseholdPlanResponse plan, string pot) => plan.Pots.Single(p => p.Pot == pot).Booked;
        decimal Actual(HouseholdPlanResponse plan, string pot) => plan.Quotas.Single(q => q.Pot == pot).Actual;
        Assert.Equal(Booked(before, PotKeys.Wants) - 200.00m, Booked(after, PotKeys.Wants));
        Assert.Equal(Booked(before, PotKeys.Savings) + 200.00m, Booked(after, PotKeys.Savings));
        Assert.Equal(Booked(before, PotKeys.Needs), Booked(after, PotKeys.Needs));
        Assert.Equal(Actual(before, PotKeys.Wants) - 10.0m, Actual(after, PotKeys.Wants));
        Assert.Equal(Actual(before, PotKeys.Savings) + 10.0m, Actual(after, PotKeys.Savings));
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
        Assert.Equal([(2026, 11), (2026, 10)],
            (await response.Content.ReadFromJsonAsync<List<HouseholdPlanMonth>>())!.Select(m => (m.Year, m.Month)));
    }

    // A8
    [Fact]
    public async Task Accounts_show_only_for_members_who_granted_them_and_the_others_are_named()
    {
        var (household, anna, ben) = await AnnaAndBenWithGiroAsync();
        var clara = await WithGiroAsync(await client.PersonAsync("Clara", withoutFirstAccount: fixture.Factory));
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

    // Spec 51, A13
    [Fact]
    public async Task Household_bookings_carry_kind_purpose_and_lines()
    {
        var (household, anna, _) = await AnnaAndBenWithGiroAsync();
        var groceries = await PositionAsync(anna.Person, "Einkauf", 400.00m, Groceries);
        await BookAsync(anna, 30.00m, PotKeys.Needs, Rent, groceries.Id);
        await BookAsync(anna, 20.00m, PotKeys.Needs, Groceries);

        var plan = await HouseholdPlanAsync(anna.Person, household.Id);

        var onPosition = Assert.Single(plan.Bookings!, b => b.Amount == 30.00m);
        Assert.Equal(BookingKinds.Expense, onPosition.Kind);
        Assert.Equal("Einkauf", onPosition.Purpose);
        var line = Assert.Single(onPosition.Lines!);
        Assert.Equal(groceries.Id, line.PositionId);
        Assert.Equal(30.00m, line.Amount);
        Assert.False(string.IsNullOrEmpty(line.Category));
        var free = Assert.Single(plan.Bookings!, b => b.Amount == 20.00m);
        Assert.Equal(Assert.Single(free.Lines!).Category, free.Purpose);
    }
}
