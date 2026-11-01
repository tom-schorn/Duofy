using System.Net;
using System.Net.Http.Json;
using Duofy.Domain;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.HouseholdDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Sparziele über die API mit echter Datenbank (Issue 30 und 32): anlegen, Stand aus zugeordneten Buchungszeilen,
/// Einzahlungen und Entnahmen, gemeinsames Sparkonto, Rechte im Bereich Sparen, Plan bei erreichtem Ziel und die Rate
/// aus dem Vertrag. "Heute" ist der 24. Oktober 2026 in Berlin.
/// </summary>
[Collection(ApiCollection.Name)]
public class SavingsGoalTests : IDisposable
{
    private const string Goals = "/api/v1/savings-goals";
    private const string Bookings = "/api/v1/bookings";
    private const string Commitments = "/api/v1/commitments";
    private const string Plans = "/api/v1/plans";

    private static readonly DateOnly Today = new(2026, 10, 24);
    private static readonly DateOnly Opening = new(2026, 1, 1);
    private static readonly Guid Groceries = CategoryCatalog.IdFor("household.groceries");
    private static readonly Guid Rent = CategoryCatalog.IdFor("housing.rent");

    private readonly ApiFixture fixture;
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public SavingsGoalTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        var now = new FixedTime(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(now)));
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

    private async Task<Guid> AccountAsync(Person owner, string name, string type = AccountTypes.Checking, decimal opening = 5000m)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, "/api/v1/accounts", new CreateAccountRequest(name, type, opening, Opening));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<SavingsGoalResponse> GoalAsync(Person owner, string name = "Urlaub", decimal? target = 1000m, Guid? accountId = null)
    {
        var response = await client.SendAsync(owner, HttpMethod.Post, Goals, new CreateSavingsGoalRequest(name, target, AccountId: accountId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
    }

    private async Task<SavingsGoalResponse> GoalOfAsync(Person owner, Guid id)
    {
        var list = await client.SendAsync(owner, HttpMethod.Get, Goals);
        return (await list.Content.ReadFromJsonAsync<List<SavingsGoalResponse>>())!.Single(g => g.Id == id);
    }

    /// <summary>Eine Umbuchung von <paramref name="from"/> auf <paramref name="to"/>, dem Ziel zugeordnet.</summary>
    private static SaveBookingRequest Transfer(Guid from, Guid to, decimal amount, string sourcePot, string targetPot, Guid? goal, Guid? ownerId = null) =>
        new(BookingKinds.Transfer, from, to, Today, amount, null,
            [new SaveBookingLine(amount, sourcePot, null, SavingsGoalId: goal)], TargetPot: targetPot, OwnerId: ownerId);

    private async Task<HttpResponseMessage> BookAsync(Person owner, SaveBookingRequest request) =>
        await client.SendAsync(owner, HttpMethod.Post, Bookings, request);

    // A1 (Issue 30): Ziele anlegen und sehen.
    [Fact]
    public async Task A_goal_is_created_with_balance_zero_and_listed_by_name()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings);

        var holiday = await GoalAsync(anna, "Urlaub", 1200m, savings);
        await GoalAsync(anna, "Auto", null);

        var list = (await (await client.SendAsync(anna, HttpMethod.Get, Goals)).Content.ReadFromJsonAsync<List<SavingsGoalResponse>>())!;
        Assert.Equal(["Auto", "Urlaub"], list.Select(g => g.Name));
        Assert.Equal((1200m, 0m, false, savings), (holiday.TargetAmount, holiday.Balance, holiday.Reached, holiday.AccountId));
    }

    [Theory]
    [InlineData("", 100, ErrorCodes.SavingsGoalNameRequired)]
    [InlineData("   ", 100, ErrorCodes.SavingsGoalNameRequired)]
    [InlineData("Urlaub", 0, ErrorCodes.SavingsGoalTargetInvalid)]
    [InlineData("Urlaub", -5, ErrorCodes.SavingsGoalTargetInvalid)]
    [InlineData("Urlaub", 1.005, ErrorCodes.SavingsGoalTargetInvalid)]
    public async Task Invalid_input_answers_400_with_its_code(string name, double target, string code)
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var response = await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest(name, (decimal)target));

        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, await response.ReadCodeAsync()));
    }

    [Fact]
    public async Task A_name_of_101_characters_and_a_foreign_account_are_refused()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var bensAccount = await AccountAsync(ben, "Bens Konto");

        var tooLong = await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest(new string('x', 101)));
        var foreign = await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest("Urlaub", AccountId: bensAccount));

        Assert.Equal(ErrorCodes.SavingsGoalNameTooLong, await tooLong.ReadCodeAsync());
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.AccountNotFound), (foreign.StatusCode, await foreign.ReadCodeAsync()));
    }

    // A2: Einzahlungen und Entnahmen werden einem Ziel zugeordnet; der Stand ist ihre Summe.
    [Fact]
    public async Task Deposits_add_and_withdrawals_subtract_from_the_balance_of_the_goal()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna, "Urlaub", 1000m, savings);

        (await BookAsync(anna, Transfer(giro, savings, 300m, PotKeys.Wants, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();
        (await BookAsync(anna, Transfer(giro, savings, 150m, PotKeys.Needs, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();
        (await BookAsync(anna, Transfer(savings, giro, 100m, PotKeys.Savings, PotKeys.Wants, goal.Id))).EnsureSuccessStatusCode();

        var seen = await GoalOfAsync(anna, goal.Id);
        Assert.Equal((350m, false), (seen.Balance, seen.Reached));
    }

    // Ein Ausgabe im Topf Sparen ist ebenfalls eine Einzahlung.
    [Fact]
    public async Task An_expense_in_the_pot_savings_counts_as_a_deposit()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var goal = await GoalAsync(anna);
        var expense = new SaveBookingRequest(BookingKinds.Expense, giro, null, Today, 40m, null,
            [new SaveBookingLine(40m, PotKeys.Savings, CategoryCatalog.IdFor("finance.savings"), SavingsGoalId: goal.Id)]);

        (await BookAsync(anna, expense)).EnsureSuccessStatusCode();

        Assert.Equal(40m, (await GoalOfAsync(anna, goal.Id)).Balance);
    }

    // A3: Mehrere Ziele teilen sich ein Sparkonto; jedes zählt nur das Seine.
    [Fact]
    public async Task Two_goals_can_share_one_savings_account_and_count_separately()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var holiday = await GoalAsync(anna, "Urlaub", 1000m, savings);
        var car = await GoalAsync(anna, "Auto", 5000m, savings);

        (await BookAsync(anna, Transfer(giro, savings, 200m, PotKeys.Wants, PotKeys.Savings, holiday.Id))).EnsureSuccessStatusCode();
        (await BookAsync(anna, Transfer(giro, savings, 75m, PotKeys.Wants, PotKeys.Savings, car.Id))).EnsureSuccessStatusCode();

        Assert.Equal((200m, 75m), ((await GoalOfAsync(anna, holiday.Id)).Balance, (await GoalOfAsync(anna, car.Id)).Balance));
        Assert.Equal(savings, (await GoalOfAsync(anna, car.Id)).AccountId);
        Assert.Equal(savings, (await GoalOfAsync(anna, holiday.Id)).AccountId);
    }

    [Fact]
    public async Task The_goal_is_reached_when_the_balance_hits_the_target()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna, "Laptop", 500m, savings);

        (await BookAsync(anna, Transfer(giro, savings, 499.99m, PotKeys.Wants, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();
        Assert.False((await GoalOfAsync(anna, goal.Id)).Reached);
        (await BookAsync(anna, Transfer(giro, savings, 0.01m, PotKeys.Wants, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();

        Assert.True((await GoalOfAsync(anna, goal.Id)).Reached);
    }

    // Zeilen, die kein Geld in den Topf Sparen oder aus ihm bewegen, lassen sich keinem Ziel zuordnen.
    [Fact]
    public async Task A_line_that_moves_no_savings_cannot_be_assigned_to_a_goal()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var goal = await GoalAsync(anna);
        var groceries = new SaveBookingRequest(BookingKinds.Expense, giro, null, Today, 20m, null,
            [new SaveBookingLine(20m, PotKeys.Needs, Groceries, SavingsGoalId: goal.Id)]);

        var response = await BookAsync(anna, groceries);

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.SavingsGoalBookingInvalid), (response.StatusCode, await response.ReadCodeAsync()));
        Assert.Equal(0m, (await GoalOfAsync(anna, goal.Id)).Balance);
    }

    // Fremde Ziele sehen aus wie unbekannte (404), auch beim Buchen.
    [Fact]
    public async Task A_booking_cannot_name_the_goal_of_another_person()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var annasGoal = await GoalAsync(anna);
        var bensGiro = await AccountAsync(ben, "Giro");
        var bensSavings = await AccountAsync(ben, "Sparkonto", AccountTypes.Savings, 0m);

        var response = await BookAsync(ben, Transfer(bensGiro, bensSavings, 10m, PotKeys.Wants, PotKeys.Savings, annasGoal.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_goal_with_bookings_is_not_deleted_but_an_unused_one_is()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var used = await GoalAsync(anna, "Benutzt");
        var unused = await GoalAsync(anna, "Leer");
        (await BookAsync(anna, Transfer(giro, savings, 10m, PotKeys.Wants, PotKeys.Savings, used.Id))).EnsureSuccessStatusCode();

        var refused = await client.SendAsync(anna, HttpMethod.Delete, $"{Goals}/{used.Id}");
        var deleted = await client.SendAsync(anna, HttpMethod.Delete, $"{Goals}/{unused.Id}");

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.SavingsGoalHasBookings), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task A_goal_is_changed_and_its_target_and_account_can_be_cleared()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna, "Urlaub", 1000m, savings);

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Goals}/{goal.Id}",
            new UpdateSavingsGoalRequest(Name: "Sommerurlaub", ClearTargetAmount: true, ClearAccountId: true));

        var changed = (await response.Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
        Assert.Equal(("Sommerurlaub", (decimal?)null, (Guid?)null, false), (changed.Name, changed.TargetAmount, changed.AccountId, changed.Reached));
    }

    // Rechte: Der Bereich Sparen hat seine eigenen Stufen.
    [Fact]
    public async Task Ben_sees_annas_goals_only_with_a_savings_grant_and_creates_them_only_with_create()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var goal = await GoalAsync(anna);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Delete, Plan: AccessLevelKeys.Delete));

        var withoutGrant = await client.SendAsync(ben, HttpMethod.Get, $"{Goals}?owner={anna.Id}");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.View));
        var withView = await client.SendAsync(ben, HttpMethod.Get, $"{Goals}?owner={anna.Id}");
        var createWithView = await client.SendAsync(ben, HttpMethod.Post, Goals, new CreateSavingsGoalRequest("Neu", OwnerId: anna.Id));
        var deleteWithView = await client.SendAsync(ben, HttpMethod.Delete, $"{Goals}/{goal.Id}");

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.OwnerNotFound), (withoutGrant.StatusCode, await withoutGrant.ReadCodeAsync()));
        Assert.Equal(goal.Id, Assert.Single((await withView.Content.ReadFromJsonAsync<List<SavingsGoalResponse>>())!).Id);
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoCreateGranted), (createWithView.StatusCode, await createWithView.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, ErrorCodes.NoDeleteGranted), (deleteWithView.StatusCode, await deleteWithView.ReadCodeAsync()));
    }

    // Sicherheit: Alte Sparziele und Schulden unter den Verträgen gehören zum Bereich Sparen.
    [Fact]
    public async Task Savings_goals_and_debts_among_the_contracts_stay_hidden_without_a_savings_grant()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var rent = new CreateCommitmentRequest(CommitmentTypes.Contract, "Miete", 890m, 1, new DateOnly(2026, 11, 1), null, Rent, PotKeys.Needs, PaymentMethod: PaymentMethods.StandingOrder);
        var oldGoal = new CreateCommitmentRequest(CommitmentTypes.SavingsGoal, "Alter Sparplan", 50m, 1, new DateOnly(2026, 11, 1), null, CategoryCatalog.IdFor("finance.savings"), PotKeys.Savings, PaymentMethod: PaymentMethods.StandingOrder);
        var debt = new CreateCommitmentRequest(CommitmentTypes.Debt, "Rate", 30m, 1, new DateOnly(2026, 11, 1), null, CategoryCatalog.IdFor("finance.savings"), PotKeys.Savings, PaymentMethod: PaymentMethods.Transfer);
        (await client.SendAsync(anna, HttpMethod.Post, Commitments, rent)).EnsureSuccessStatusCode();
        var savingsResponse = await client.SendAsync(anna, HttpMethod.Post, Commitments, oldGoal);
        savingsResponse.EnsureSuccessStatusCode();
        var savingsCommitment = (await savingsResponse.Content.ReadFromJsonAsync<CommitmentResponse>())!;
        (await client.SendAsync(anna, HttpMethod.Post, Commitments, debt)).EnsureSuccessStatusCode();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Delete));

        var list = await client.SendAsync(ben, HttpMethod.Get, $"{Commitments}?owner={anna.Id}");
        var change = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{savingsCommitment.Id}", new UpdateCommitmentRequest(Name: "X"));
        var end = await client.SendAsync(ben, HttpMethod.Post, $"{Commitments}/{savingsCommitment.Id}/end");
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.View));
        var listWithSavings = await client.SendAsync(ben, HttpMethod.Get, $"{Commitments}?owner={anna.Id}");

        Assert.Equal(["Miete"], (await list.Content.ReadFromJsonAsync<List<CommitmentResponse>>())!.Select(c => c.Name));
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, end.StatusCode);
        Assert.Equal(3, (await listWithSavings.Content.ReadFromJsonAsync<List<CommitmentResponse>>())!.Count);
    }

    // Issue 30, Kriterium 4 und Issue 32: Die Rate geht an das Ziel; ist es erreicht, verlangt der Plan sie nicht mehr.
    private async Task<(Person Anna, Guid Giro, Guid Savings, SavingsGoalResponse Goal, CommitmentResponse Rate)> RateToGoalAsync(decimal target)
    {
        var anna = await client.FounderAsync("Anna");
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna, "Urlaub", target, savings);
        var request = new CreateCommitmentRequest(CommitmentTypes.Contract, "Rate Urlaub", 200m, 1, new DateOnly(2026, 11, 1), null,
            Rent, PotKeys.Wants, AccountId: giro, CounterAccountId: savings, PaymentMethod: PaymentMethods.StandingOrder, SavingsGoalId: goal.Id);
        var response = await client.SendAsync(anna, HttpMethod.Post, Commitments, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (anna, giro, savings, goal, (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!);
    }

    private async Task<PlanResponse> PlanAsync(Person person, int year, int month) =>
        (await (await client.SendAsync(person, HttpMethod.Get, $"{Plans}/{year}/{month}")).Content.ReadFromJsonAsync<PlanResponse>())!;

    [Fact]
    public async Task The_rate_of_a_contract_flows_into_the_goal_when_the_position_is_ticked()
    {
        var (anna, _, _, goal, rate) = await RateToGoalAsync(1000m);
        Assert.Equal(goal.Id, rate.SavingsGoalId);
        var position = Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);

        var tick = await client.SendAsync(anna, HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", new TickPositionRequest());

        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);
        var seen = await GoalOfAsync(anna, goal.Id);
        Assert.Equal((200m, false), (seen.Balance, seen.Reached));
        var untick = await client.SendAsync(anna, HttpMethod.Delete, $"{Plans}/2026/11/positions/{position.Id}/tick");
        Assert.Equal(HttpStatusCode.OK, untick.StatusCode);
        Assert.Equal(0m, (await GoalOfAsync(anna, goal.Id)).Balance);
    }

    [Fact]
    public async Task A_new_month_does_not_ask_for_the_rate_once_the_goal_is_reached()
    {
        var (anna, giro, savings, goal, _) = await RateToGoalAsync(500m);
        Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);
        (await BookAsync(anna, Transfer(giro, savings, 500m, PotKeys.Wants, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();

        var december = await PlanAsync(anna, 2026, 12);
        var november = await PlanAsync(anna, 2026, 11);

        Assert.Empty(december.Positions);
        // Der November war schon angelegt; der Abgleich bietet an, den Posten zu entfernen.
        Assert.Equal(PlanChangeKinds.NoLongerDue, Assert.Single(november.Changes).Kind);
    }

    [Fact]
    public async Task A_contract_with_a_goal_needs_a_target_account_and_the_goal_of_the_same_person()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var goal = await GoalAsync(anna);
        var bensGoal = await GoalAsync(ben, "Bens Ziel");
        CreateCommitmentRequest With(Guid goalId, Guid? counter) => new(
            CommitmentTypes.Contract, "Rate", 50m, 1, new DateOnly(2026, 11, 1), null, Rent, PotKeys.Wants,
            AccountId: giro, CounterAccountId: counter, PaymentMethod: PaymentMethods.StandingOrder, SavingsGoalId: goalId);

        var noTarget = await client.SendAsync(anna, HttpMethod.Post, Commitments, With(goal.Id, null));
        var foreign = await client.SendAsync(anna, HttpMethod.Post, Commitments, With(bensGoal.Id, await AccountAsync(anna, "Spar", AccountTypes.Savings, 0m)));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.CommitmentGoalInvalid), (noTarget.StatusCode, await noTarget.ReadCodeAsync()));
        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.SavingsGoalNotFound), (foreign.StatusCode, await foreign.ReadCodeAsync()));
    }

    // Sicherheit: Wer nur Buchungen ändern darf, löst keine Zuordnung zu einem Ziel und löscht keine zugeordnete Buchung.
    [Fact]
    public async Task Changing_or_deleting_an_assigned_booking_needs_the_savings_area()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna);
        var booking = (await (await BookAsync(anna, Transfer(giro, savings, 60m, PotKeys.Wants, PotKeys.Savings, goal.Id))).Content.ReadFromJsonAsync<BookingResponse>())!;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.Delete, Accounts: AccessLevelKeys.View, Plan: AccessLevelKeys.View));

        var unassign = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Transfer(giro, savings, 60m, PotKeys.Wants, PotKeys.Savings, null));
        var delete = await client.SendAsync(ben, HttpMethod.Delete, $"{Bookings}/{booking.Id}");
        // Auch derselbe Zielbezug mit anderem Betrag verändert den Stand und braucht den Bereich Sparen.
        var otherAmount = await client.SendAsync(ben, HttpMethod.Put, $"{Bookings}/{booking.Id}", Transfer(giro, savings, 90m, PotKeys.Wants, PotKeys.Savings, goal.Id));

        Assert.Equal(HttpStatusCode.NotFound, otherAmount.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unassign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(60m, (await GoalOfAsync(anna, goal.Id)).Balance);
    }

    // Sicherheit: Das Ziel eines Vertrags setzen, ändern oder lösen braucht Stufen im Bereich Sparen.
    [Fact]
    public async Task Linking_or_unlinking_a_goal_of_a_contract_needs_the_savings_area()
    {
        var (anna, ben, rate) = await TwoWithRateAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Commitments: AccessLevelKeys.Delete));

        var clear = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rate.Id}", new UpdateCommitmentRequest(ClearSavingsGoalId: true));
        var listed = (await (await client.SendAsync(ben, HttpMethod.Get, $"{Commitments}?owner={anna.Id}")).Content.ReadFromJsonAsync<List<CommitmentResponse>>())!;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.Edit));
        var clearWithSavings = await client.SendAsync(ben, HttpMethod.Patch, $"{Commitments}/{rate.Id}", new UpdateCommitmentRequest(ClearSavingsGoalId: true));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.SavingsGoalNotFound), (clear.StatusCode, await clear.ReadCodeAsync()));
        Assert.Null(Assert.Single(listed).SavingsGoalId);
        Assert.Equal(HttpStatusCode.OK, clearWithSavings.StatusCode);
        Assert.Null((await clearWithSavings.Content.ReadFromJsonAsync<CommitmentResponse>())!.SavingsGoalId);
    }

    private async Task<(Person Anna, Person Ben, CommitmentResponse Rate)> TwoWithRateAsync()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna, "Urlaub", 1000m, savings);
        var request = new CreateCommitmentRequest(CommitmentTypes.Contract, "Rate Urlaub", 200m, 1, new DateOnly(2026, 11, 1), null,
            Rent, PotKeys.Wants, AccountId: giro, CounterAccountId: savings, PaymentMethod: PaymentMethods.StandingOrder, SavingsGoalId: goal.Id);
        var response = await client.SendAsync(anna, HttpMethod.Post, Commitments, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (anna, ben, (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!);
    }

    // Sicherheit: Der Planabgleich nennt alte Sparziele und Schulden nur, wem der Bereich Sparen freigegeben ist.
    [Fact]
    public async Task The_plan_comparison_leaves_out_old_savings_contracts_without_the_savings_area()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        (await client.SendAsync(anna, HttpMethod.Get, $"{Plans}/2026/11")).EnsureSuccessStatusCode();
        var oldGoal = new CreateCommitmentRequest(CommitmentTypes.SavingsGoal, "Alter Sparplan", 50m, 1, new DateOnly(2026, 11, 1), null,
            CategoryCatalog.IdFor("finance.savings"), PotKeys.Savings, AccountId: giro, PaymentMethod: PaymentMethods.StandingOrder);
        (await client.SendAsync(anna, HttpMethod.Post, Commitments, oldGoal)).EnsureSuccessStatusCode();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete, Commitments: AccessLevelKeys.View));

        var without = (await (await client.SendAsync(ben, HttpMethod.Get, $"{Plans}/2026/11?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!;
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.View));
        var with = (await (await client.SendAsync(ben, HttpMethod.Get, $"{Plans}/2026/11?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!;

        Assert.Empty(without.Changes);
        Assert.Equal("Alter Sparplan", Assert.Single(with.Changes).Label);
    }

    // Tom 01.11.: "Schon angespart" ist ein einmaliger Startwert; der Stand ist er plus Einzahlungen minus Entnahmen.
    [Fact]
    public async Task The_start_value_counts_into_the_balance_and_creates_no_booking()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);

        var created = await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest("Urlaub", 1000m, AccountId: savings, StartAmount: 250m));
        var goal = (await created.Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
        (await BookAsync(anna, Transfer(giro, savings, 100m, PotKeys.Wants, PotKeys.Savings, goal.Id))).EnsureSuccessStatusCode();
        (await BookAsync(anna, Transfer(savings, giro, 30m, PotKeys.Savings, PotKeys.Wants, goal.Id))).EnsureSuccessStatusCode();

        var seen = await GoalOfAsync(anna, goal.Id);
        Assert.Equal((250m, 320m, false), (goal.Balance, seen.Balance, seen.Reached));
        Assert.Equal(250m, seen.StartAmount);
        Assert.Equal(2, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.OwnerId == anna.Id)));
    }

    [Fact]
    public async Task A_start_value_alone_can_reach_the_goal_and_without_one_it_is_zero()
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var reached = (await (await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest("Fertig", 200m, StartAmount: 200m))).Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
        var plain = await GoalAsync(anna, "Neu", 200m);

        Assert.True(reached.Reached);
        Assert.Equal((0m, false), (plain.StartAmount, plain.Reached));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.005)]
    public async Task A_negative_start_value_or_one_with_three_decimals_is_refused(double start)
    {
        var (anna, _) = await client.AnnaAndBenAsync();

        var response = await client.SendAsync(anna, HttpMethod.Post, Goals, new CreateSavingsGoalRequest("Urlaub", StartAmount: (decimal)start));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.SavingsGoalStartInvalid), (response.StatusCode, await response.ReadCodeAsync()));
    }

    [Fact]
    public async Task The_start_value_can_be_changed_later_and_changes_the_balance()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var goal = await GoalAsync(anna, "Urlaub", 1000m);

        var response = await client.SendAsync(anna, HttpMethod.Patch, $"{Goals}/{goal.Id}", new UpdateSavingsGoalRequest(StartAmount: 400m));

        var changed = (await response.Content.ReadFromJsonAsync<SavingsGoalResponse>())!;
        Assert.Equal((400m, 400m), (changed.StartAmount, changed.Balance));
    }

    [Fact]
    public async Task A_start_value_that_reaches_the_goal_takes_the_rate_out_of_the_next_plan()
    {
        var (anna, _, _, goal, _) = await RateToGoalAsync(500m);
        Assert.Single((await PlanAsync(anna, 2026, 12)).Positions);

        (await client.SendAsync(anna, HttpMethod.Patch, $"{Goals}/{goal.Id}", new UpdateSavingsGoalRequest(StartAmount: 500m))).EnsureSuccessStatusCode();

        Assert.Empty((await PlanAsync(anna, 2027, 1)).Positions);
    }

    // Sicherheit: Eine Rate an ein Sparziel abzuhaken oder den Haken zu lösen braucht den Bereich Sparen.
    [Fact]
    public async Task Ticking_or_unticking_a_rate_that_feeds_a_goal_needs_the_savings_area()
    {
        var (anna, ben, rate) = await TwoWithRateAsync();
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.Delete, Book: AccessLevelKeys.Delete, Accounts: AccessLevelKeys.View, Commitments: AccessLevelKeys.View));
        var position = Assert.Single((await (await client.SendAsync(ben, HttpMethod.Get, $"{Plans}/2026/11?owner={anna.Id}")).Content.ReadFromJsonAsync<PlanResponse>())!.Positions);

        var tick = await client.SendAsync(ben, HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick?owner={anna.Id}", new TickPositionRequest());
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.Create));
        var tickWithSavings = await client.SendAsync(ben, HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick?owner={anna.Id}", new TickPositionRequest());
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Savings: AccessLevelKeys.None));
        var untick = await client.SendAsync(ben, HttpMethod.Delete, $"{Plans}/2026/11/positions/{position.Id}/tick?owner={anna.Id}");

        Assert.Equal(HttpStatusCode.NotFound, tick.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tickWithSavings.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, untick.StatusCode);
        Assert.Equal(200m, (await GoalOfAsync(anna, rate.SavingsGoalId!.Value)).Balance);
    }

    // Die Zuordnung hängt an der Buchungszeile und steht in der Antwort.
    [Fact]
    public async Task The_booking_answer_carries_the_goal_of_its_line()
    {
        var (anna, _) = await client.AnnaAndBenAsync();
        var giro = await AccountAsync(anna, "Giro");
        var savings = await AccountAsync(anna, "Sparkonto", AccountTypes.Savings, 0m);
        var goal = await GoalAsync(anna);

        var response = await BookAsync(anna, Transfer(giro, savings, 25m, PotKeys.Wants, PotKeys.Savings, goal.Id));

        var booking = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(goal.Id, Assert.Single(booking.Lines).SavingsGoalId);
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.SavingsGoals.CountAsync(g => g.Id == goal.Id)));
    }
}
