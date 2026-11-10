using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Der Monatsplan über die API mit echter Datenbank (Spec 66, A1 bis A16): Monat anlegen, Abgleich
/// mit den Verträgen, einmalige Posten, Abhaken, Posten je Buchungszeile, Planmonat, Quoten,
/// Übertrag und fremde Kennungen. "Heute" ist hier immer der 24. Oktober 2026 in Berlin.
/// </summary>
[Collection(ApiCollection.Name)]
public class PlanTests : IDisposable
{
    private const string Plans = "/api/v1/plans";
    private const string Bookings = "/api/v1/bookings";

    private static readonly DateOnly September1 = new(2026, 9, 1);
    private static readonly DateOnly January1 = new(2026, 1, 1);
    private static readonly DateOnly October5 = new(2026, 10, 5);

    private readonly ApiFixture fixture;
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public PlanTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        // 24. Oktober 2026, 10 Uhr in Berlin (Sommerzeit, also 8 Uhr UTC).
        var today = new FixedTime(new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(today)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    /// <summary>Eine angemeldete Person mit Girokonto (Standardkonto) und den Kategorien der Tests.</summary>
    private sealed record Person(
        string Token, Guid Giro, Guid Rent, Guid Groceries, Guid PersonalCare, Guid Gifts, Guid Salary, Guid Savings);

    private async Task<Person> SignUpAsync()
    {
        var (_, tokens) = await client.SignUpAsync(fixture.Factory);
        var categories = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/categories", tokens.AccessToken));
        var all = (await categories.Content.ReadFromJsonAsync<List<CategoryResponse>>())!;
        Guid Id(string key) => all.Single(c => c.Key == key).Id;
        var person = new Person(tokens.AccessToken, Guid.Empty, Id("housing.rent"), Id("household.groceries"),
            Id("household.personal_care"), Id("personal.gifts"), Id("income.earned"), Id("finance.savings"));
        return person with { Giro = await AccountAsync(person, "Giro", 1000.00m) };
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Person person, object? body = null) =>
        client.SendAsync(Authorized(method, path, person.Token, body));

    private async Task<Guid> AccountAsync(Person person, string name, decimal openingBalance, string type = AccountTypes.Checking)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/accounts", person,
            new CreateAccountRequest(name, type, openingBalance, September1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<CommitmentResponse> CommitmentAsync(Person person, CreateCommitmentRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/commitments", person,
            request with { PaymentMethod = request.PaymentMethod ?? PaymentMethods.Other });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CommitmentResponse>())!;
    }

    private Task<CommitmentResponse> RentAsync(Person person, decimal amount = 890.00m) =>
        CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Miete", amount, 1, January1, null, person.Rent, PotKeys.Needs));

    private async Task<PlanResponse> PlanAsync(Person person, int year, int month)
    {
        var response = await SendAsync(HttpMethod.Get, $"{Plans}/{year}/{month}", person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlanResponse>())!;
    }

    private async Task<PlanPositionResponse> OneOffAsync(
        Person person, int year, int month, string label, decimal amount, Guid category, string pot = PotKeys.Needs, int day = 10)
    {
        var response = await SendAsync(HttpMethod.Post, $"{Plans}/{year}/{month}/positions", person,
            new CreatePositionRequest(label, amount, day, category, pot));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
    }

    private async Task<BookingResponse> BookAsync(Person person, SaveBookingRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Bookings, person, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private static SaveBookingRequest Expense(Person person, decimal amount, Guid? position = null, DateOnly? date = null, Guid? category = null) =>
        new(BookingKinds.Expense, person.Giro, null, date ?? October5, amount, null,
            [new SaveBookingLine(amount, PotKeys.Needs, category ?? person.Groceries, position)]);

    private static SaveBookingRequest Income(Person person, decimal amount, DateOnly? date = null, int? planYear = null, int? planMonth = null) =>
        new(BookingKinds.Income, person.Giro, null, date ?? October5, amount, null,
            [new SaveBookingLine(amount, PotKeys.Income, person.Salary)], planYear, planMonth);

    private async Task<List<BookingResponse>> BookingsInAsync(Person person, int year, int month)
    {
        var response = await SendAsync(HttpMethod.Get, $"{Bookings}?year={year}&month={month}", person);
        return (await response.Content.ReadFromJsonAsync<BookingMonthResponse>())!.Bookings.ToList();
    }

    /// <summary>Eine Buchung des Übertrags, direkt aus der Datenbank: Das Paar steht in keiner Liste der Buchungen.</summary>
    private Task<BookingResponse> CarryOverBookingAsync(Person person, string note) =>
        fixture.Factory.WithDbAsync(async db => BookingEndpoints.ToResponse(
            await BookingEndpoints.WithLines(db).AsNoTracking().SingleAsync(b => b.AccountId == person.Giro && b.Note == note)));

    private static PlanPositionResponse Position(PlanResponse plan, string label) => plan.Positions.Single(p => p.Label == label);

    [Fact]
    public async Task Plans_need_a_signed_in_person()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Plans}/2026/11")).StatusCode);
    }

    [Fact]
    public async Task A_month_outside_the_calendar_or_range_is_refused()
    {
        var person = await SignUpAsync();

        var thirteen = await SendAsync(HttpMethod.Get, $"{Plans}/2026/13", person);
        var tooEarly = await SendAsync(HttpMethod.Get, $"{Plans}/1999/12", person);

        Assert.Equal(HttpStatusCode.BadRequest, thirteen.StatusCode);
        Assert.Equal(ErrorCodes.MonthInvalid, await thirteen.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
    }

    // A1
    [Fact]
    public async Task Opening_a_month_creates_it_once_from_the_due_contracts()
    {
        var person = await SignUpAsync();
        var rent = await RentAsync(person);

        var first = await PlanAsync(person, 2026, 11);
        var second = await PlanAsync(person, 2026, 11);

        var position = Assert.Single(first.Positions);
        Assert.Equal("Miete", position.Label);
        Assert.Equal(890.00m, position.AmountPlanned);
        Assert.Equal(1, position.DueDay);
        Assert.Equal(rent.Id, position.CommitmentId);
        Assert.Equal(PotKeys.Needs, position.Pot);
        Assert.Equal(position.Id, Assert.Single(second.Positions).Id);
        Assert.Equal(first.Id, second.Id);
    }

    // A24
    [Fact]
    public async Task A_month_before_the_first_account_opening_is_created_empty()
    {
        var person = await SignUpAsync();
        await RentAsync(person);

        var august = await PlanAsync(person, 2026, 8);
        var applied = await SendAsync(HttpMethod.Post, $"{Plans}/2026/8/changes/apply", person, new ApplyPlanChangesRequest());

        Assert.Empty(august.Positions);
        Assert.Empty(august.Changes);
        Assert.Empty((await applied.Content.ReadFromJsonAsync<PlanResponse>())!.Positions);
    }

    // A2, A3
    [Fact]
    public async Task A_changed_contract_does_not_rewrite_the_month_but_is_offered_as_a_change()
    {
        var person = await SignUpAsync();
        var rent = await RentAsync(person);
        await PlanAsync(person, 2026, 11);

        var patch = await SendAsync(HttpMethod.Patch, $"/api/v1/commitments/{rent.Id}", person, new UpdateCommitmentRequest(Amount: 950.00m));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var november = await PlanAsync(person, 2026, 11);

        Assert.Equal(890.00m, Assert.Single(november.Positions).AmountPlanned);
        var change = Assert.Single(november.Changes);
        Assert.Equal(PlanChangeKinds.Changed, change.Kind);
        Assert.Equal("Miete", change.Label);
        Assert.False(change.Protected);
        var field = Assert.Single(change.Fields);
        Assert.Equal(PlanFields.Amount, field.Field);
        Assert.Equal("890.00", field.Before);
        Assert.Equal("950.00", field.After);

        var applied = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/changes/apply", person, new ApplyPlanChangesRequest());
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var after = (await applied.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Equal(950.00m, Assert.Single(after.Positions).AmountPlanned);
        Assert.Empty(after.Changes);
    }

    // A4
    [Fact]
    public async Task Applying_all_changes_leaves_a_ticked_position_alone_until_it_is_picked()
    {
        var person = await SignUpAsync();
        var rent = await RentAsync(person);
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", person, new TickPositionRequest())).StatusCode);
        await SendAsync(HttpMethod.Patch, $"/api/v1/commitments/{rent.Id}", person, new UpdateCommitmentRequest(Amount: 950.00m));

        var all = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/changes/apply", person, new ApplyPlanChangesRequest());
        var afterAll = (await all.Content.ReadFromJsonAsync<PlanResponse>())!;

        Assert.Equal(890.00m, Assert.Single(afterAll.Positions).AmountPlanned);
        Assert.True(Assert.Single(afterAll.Changes).Protected);

        var picked = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/changes/apply", person,
            new ApplyPlanChangesRequest([new PlanChangeItem(position.Id, null)]));
        var afterPick = (await picked.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Equal(950.00m, Assert.Single(afterPick.Positions).AmountPlanned);
        Assert.NotNull(Assert.Single(afterPick.Positions).DoneAt);
    }

    // A5
    [Fact]
    public async Task A_contract_created_after_the_month_is_offered_as_new()
    {
        var person = await SignUpAsync();
        await RentAsync(person);
        await PlanAsync(person, 2026, 11);
        var gym = await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Fitness", 30.00m, 1, new DateOnly(2026, 11, 15), null, person.Gifts, PotKeys.Wants));

        var november = await PlanAsync(person, 2026, 11);
        var change = Assert.Single(november.Changes);
        Assert.Equal(PlanChangeKinds.New, change.Kind);
        Assert.Equal(gym.Id, change.CommitmentId);
        Assert.Equal("Fitness", change.Label);

        var applied = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/changes/apply", person,
            new ApplyPlanChangesRequest([new PlanChangeItem(null, gym.Id)]));
        var after = (await applied.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Contains(after.Positions, p => p.CommitmentId == gym.Id && p.DueDay == 15);
        Assert.Empty(after.Changes);
    }

    [Fact]
    public async Task An_ended_contract_is_offered_for_removal()
    {
        var person = await SignUpAsync();
        var rent = await RentAsync(person);
        await PlanAsync(person, 2026, 11);
        await SendAsync(HttpMethod.Post, $"/api/v1/commitments/{rent.Id}/end", person);

        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal(PlanChangeKinds.NoLongerDue, Assert.Single(november.Changes).Kind);

        var applied = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/changes/apply", person, new ApplyPlanChangesRequest());
        Assert.Empty((await applied.Content.ReadFromJsonAsync<PlanResponse>())!.Positions);
    }

    // A6
    [Fact]
    public async Task A_one_off_position_belongs_to_its_month_only()
    {
        var person = await SignUpAsync();

        var gift = await OneOffAsync(person, 2026, 11, "Geschenk", 50.00m, person.Gifts, PotKeys.Wants, day: 20);

        Assert.Null(gift.CommitmentId);
        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal(20, Position(november, "Geschenk").DueDay);
        Assert.DoesNotContain((await PlanAsync(person, 2026, 12)).Positions, p => p.Label == "Geschenk");
    }

    [Fact]
    public async Task A_one_off_position_follows_the_rules_for_name_day_pot_and_category()
    {
        var person = await SignUpAsync();
        async Task<string?> CodeAsync(CreatePositionRequest request)
        {
            var response = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions", person, request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return await response.ReadCodeAsync();
        }

        Assert.Equal(ErrorCodes.PositionLabelRequired, await CodeAsync(new(" ", 50m, 20, person.Gifts, PotKeys.Wants)));
        Assert.Equal(ErrorCodes.PositionLabelTooLong, await CodeAsync(new(new string('x', 101), 50m, 20, person.Gifts, PotKeys.Wants)));
        Assert.Equal(ErrorCodes.AmountInvalid, await CodeAsync(new("Geschenk", 0m, 20, person.Gifts, PotKeys.Wants)));
        // Der November hat 30 Tage.
        Assert.Equal(ErrorCodes.PositionDayInvalid, await CodeAsync(new("Geschenk", 50m, 31, person.Gifts, PotKeys.Wants)));
        Assert.Equal(ErrorCodes.PositionPotInvalid, await CodeAsync(new("Geschenk", 50m, 20, person.Gifts, "luxury")));
        Assert.Equal(ErrorCodes.PositionCategoryInvalid, await CodeAsync(new("Geschenk", 50m, 20, person.Salary, PotKeys.Wants)));
        Assert.Equal(ErrorCodes.CategoryNotFound, await CodeAsync(new("Geschenk", 50m, 20, Guid.NewGuid(), PotKeys.Wants)));
    }

    [Fact]
    public async Task Changing_a_contract_position_by_hand_marks_it()
    {
        var person = await SignUpAsync();
        await RentAsync(person);
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);

        var response = await SendAsync(HttpMethod.Patch, $"{Plans}/2026/11/positions/{position.Id}", person,
            new UpdatePositionRequest(Amount: 900.00m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changed = (await response.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
        Assert.Equal(900.00m, changed.AmountPlanned);
        Assert.True(changed.ChangedByHand);
        // Der Vertrag hat weiter 890; der Unterschied wird gezeigt, aber geschützt.
        Assert.True(Assert.Single((await PlanAsync(person, 2026, 11)).Changes).Protected);
    }

    // A7
    [Fact]
    public async Task Ticking_without_bookings_books_the_position_and_unticking_removes_the_booking()
    {
        var person = await SignUpAsync();
        await RentAsync(person);
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);

        var tick = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", person, new TickPositionRequest());

        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);
        var ticked = (await tick.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
        Assert.NotNull(ticked.DoneAt);
        Assert.Equal(890.00m, ticked.Booked);
        // Der November liegt in der Zukunft: Die Buchung trägt das heutige Datum und den Planmonat November.
        var booking = Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.Equal(ticked.TickBookingId, booking.Id);
        Assert.Equal(890.00m, booking.Amount);
        Assert.Equal(person.Giro, booking.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 24), booking.Date);
        Assert.Equal((2026, 11), (booking.PlanYear, booking.PlanMonth));
        Assert.Equal(position.Id, Assert.Single(booking.Lines).PositionId);
        Assert.Equal(person.Rent, booking.Lines[0].CategoryId);

        var untick = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/11/positions/{position.Id}/tick", person);

        Assert.Equal(HttpStatusCode.OK, untick.StatusCode);
        var open = (await untick.Content.ReadFromJsonAsync<PlanPositionResponse>())!;
        Assert.Null(open.DoneAt);
        Assert.Equal(0m, open.Booked);
        Assert.Empty(await BookingsInAsync(person, 2026, 10));
    }

    [Fact]
    public async Task Ticking_a_past_position_books_it_on_its_due_day()
    {
        var person = await SignUpAsync();
        await RentAsync(person);
        var position = Assert.Single((await PlanAsync(person, 2026, 10)).Positions);

        await SendAsync(HttpMethod.Post, $"{Plans}/2026/10/positions/{position.Id}/tick", person, new TickPositionRequest());

        Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single(await BookingsInAsync(person, 2026, 10)).Date);
    }

    // A8
    [Fact]
    public async Task Ticking_a_position_with_bookings_books_only_the_balance_that_was_asked_for()
    {
        var person = await SignUpAsync();
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 600.00m, person.Groceries);
        await BookAsync(person, Expense(person, 250.00m, groceries.Id));
        await BookAsync(person, Expense(person, 300.00m, groceries.Id));
        var path = $"{Plans}/2026/10/positions/{groceries.Id}/tick";

        var zero = (await (await SendAsync(HttpMethod.Post, path, person, new TickPositionRequest(Amount: 0m)))
            .Content.ReadFromJsonAsync<PlanPositionResponse>())!;

        Assert.NotNull(zero.DoneAt);
        Assert.Equal(550.00m, zero.Booked);
        Assert.Null(zero.TickBookingId);
        Assert.Equal(2, (await BookingsInAsync(person, 2026, 10)).Count);

        await SendAsync(HttpMethod.Delete, path, person);
        var fifty = (await (await SendAsync(HttpMethod.Post, path, person, new TickPositionRequest(Amount: 50.00m)))
            .Content.ReadFromJsonAsync<PlanPositionResponse>())!;

        Assert.Equal(600.00m, fifty.Booked);
        Assert.Contains(await BookingsInAsync(person, 2026, 10), b => b.Id == fifty.TickBookingId && b.Amount == 50.00m);
    }

    // A9
    [Fact]
    public async Task Ticking_a_savings_goal_with_a_target_account_books_a_transfer()
    {
        var person = await SignUpAsync();
        var savingsAccount = await AccountAsync(person, "Sparkonto", 0m, AccountTypes.Savings);
        await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.SavingsGoal, "Urlaub", 100.00m, 1, January1, null, person.Savings, null,
            CounterAccountId: savingsAccount));
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);

        await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", person, new TickPositionRequest());

        var booking = Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.Equal(BookingKinds.Transfer, booking.Kind);
        Assert.Equal(person.Giro, booking.AccountId);
        Assert.Equal(savingsAccount, booking.CounterAccountId);
        Assert.Equal(position.Id, Assert.Single(booking.Lines).PositionId);
        // Spec 72, R9: Beim Abhaken tragen beide Seiten den Topf des Postens (hier Sparen).
        Assert.Equal((PotKeys.Savings, PotKeys.Savings), (booking.Lines[0].Pot, booking.TargetPot));
        Assert.Equal(100.00m, Position(await PlanAsync(person, 2026, 11), "Urlaub").Booked);
    }

    [Fact]
    public async Task Ticking_a_contract_with_a_target_account_fills_its_position_whatever_the_pot()
    {
        var person = await SignUpAsync();
        var cash = await AccountAsync(person, "Bargeld", 0m, AccountTypes.Cash);
        await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Umschlag Lebensmittel", 400.00m, 1, January1, null, person.Groceries, PotKeys.Needs,
            CounterAccountId: cash));
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);

        var tick = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", person, new TickPositionRequest());

        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);
        Assert.Equal(400.00m, (await tick.Content.ReadFromJsonAsync<PlanPositionResponse>())!.Booked);
        var booking = Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.Equal((BookingKinds.Transfer, cash), (booking.Kind, booking.CounterAccountId));
        Assert.Equal(position.Id, Assert.Single(booking.Lines).PositionId);
        Assert.Equal((PotKeys.Needs, PotKeys.Needs), (booking.Lines[0].Pot, booking.TargetPot));
    }

    // A19
    [Fact]
    public async Task The_transfer_of_a_ticked_position_with_a_target_account_can_be_changed_with_its_position()
    {
        var person = await SignUpAsync();
        var cash = await AccountAsync(person, "Bargeld", 0m, AccountTypes.Cash);
        await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Umschlag Lebensmittel", 400.00m, 1, January1, null, person.Groceries, PotKeys.Needs,
            CounterAccountId: cash));
        var position = Assert.Single((await PlanAsync(person, 2026, 11)).Positions);
        await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/positions/{position.Id}/tick", person, new TickPositionRequest());
        var booking = Assert.Single(await BookingsInAsync(person, 2026, 10));

        var put = await SendAsync(HttpMethod.Put, $"{Bookings}/{booking.Id}", person,
            new SaveBookingRequest(BookingKinds.Transfer, person.Giro, cash, booking.Date, 380.00m, booking.Note,
                [new SaveBookingLine(380.00m, PotKeys.Needs, null, position.Id)], 2026, 11, TargetPot: PotKeys.Needs));

        Assert.True(put.StatusCode == HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        Assert.Equal(position.Id, Assert.Single((await put.Content.ReadFromJsonAsync<BookingResponse>())!.Lines).PositionId);
        Assert.Equal(380.00m, Position(await PlanAsync(person, 2026, 11), "Umschlag Lebensmittel").Booked);
    }

    // A10
    [Fact]
    public async Task Each_line_of_a_split_booking_fills_its_own_position()
    {
        var person = await SignUpAsync();
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 600.00m, person.Groceries);
        var drugstore = await OneOffAsync(person, 2026, 10, "Drogerie", 50.00m, person.PersonalCare);

        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, person.Giro, null, October5, 80.00m, null,
        [
            new SaveBookingLine(60.00m, PotKeys.Needs, person.Groceries, groceries.Id),
            new SaveBookingLine(20.00m, PotKeys.Needs, person.PersonalCare, drugstore.Id),
        ]));

        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal(60.00m, Position(october, "Lebensmittel").Booked);
        Assert.Equal(20.00m, Position(october, "Drogerie").Booked);
    }

    [Fact]
    public async Task A_line_may_only_fill_a_position_of_the_same_kind_and_plan_month()
    {
        var person = await SignUpAsync();
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 600.00m, person.Groceries);
        var salary = await OneOffAsync(person, 2026, 10, "Gehalt", 2400.00m, person.Salary, PotKeys.Income);

        var wrongKind = await SendAsync(HttpMethod.Post, Bookings, person, Expense(person, 20.00m, salary.Id));
        var otherMonth = await SendAsync(HttpMethod.Post, Bookings, person,
            Expense(person, 20.00m, groceries.Id) with { PlanYear = 2026, PlanMonth = 11 });
        var unknown = await SendAsync(HttpMethod.Post, Bookings, person, Expense(person, 20.00m, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, wrongKind.StatusCode);
        Assert.Equal(ErrorCodes.LinePositionInvalid, await wrongKind.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, otherMonth.StatusCode);
        Assert.Equal(ErrorCodes.PlanMonthInvalid, await otherMonth.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // A11
    [Fact]
    public async Task A_booking_counts_in_its_plan_month_while_the_balance_follows_its_date()
    {
        var person = await SignUpAsync();

        var salary = await BookAsync(person, Income(person, 2400.00m, new DateOnly(2026, 9, 25), 2026, 10));
        var tooFar = await SendAsync(HttpMethod.Post, Bookings, person, Income(person, 2400.00m, new DateOnly(2026, 9, 25), 2026, 12));

        Assert.Equal((2026, 10), (salary.PlanYear, salary.PlanMonth));
        Assert.Equal(2400.00m, (await PlanAsync(person, 2026, 10)).BookedIncome);
        Assert.Equal(0m, (await PlanAsync(person, 2026, 9)).BookedIncome);
        Assert.Contains(await BookingsInAsync(person, 2026, 9), b => b.Id == salary.Id);
        Assert.Equal(3400.00m, await BalanceOnAsync(person.Giro, new DateOnly(2026, 9, 30)));
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);
        Assert.Equal(ErrorCodes.PlanMonthInvalid, await tooFar.ReadCodeAsync());
    }

    [Fact]
    public async Task Without_a_plan_month_a_booking_counts_in_the_month_of_its_date()
    {
        var person = await SignUpAsync();

        var booking = await BookAsync(person, Expense(person, 20.00m));

        Assert.Equal((2026, 10), (booking.PlanYear, booking.PlanMonth));
    }

    // A12
    [Fact]
    public async Task Quotas_start_at_50_30_20_and_a_new_month_copies_the_settings()
    {
        var person = await SignUpAsync();
        var start = await SendAsync(HttpMethod.Get, "/api/v1/users/me/quotas", person);
        Assert.Equal(new QuotaSettings(50, 30, 20), await start.Content.ReadFromJsonAsync<QuotaSettings>());
        await PlanAsync(person, 2026, 10);

        var put = await SendAsync(HttpMethod.Put, "/api/v1/users/me/quotas", person, new QuotaSettings(60, 20, 20));
        var wrong = await SendAsync(HttpMethod.Put, "/api/v1/users/me/quotas", person, new QuotaSettings(60, 30, 20));
        var negative = await SendAsync(HttpMethod.Put, "/api/v1/users/me/quotas", person, new QuotaSettings(110, -10, 0));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new[] { 60, 20, 20 }, (await PlanAsync(person, 2026, 12)).Quotas.Select(q => q.Target));
        Assert.Equal(new[] { 50, 30, 20 }, (await PlanAsync(person, 2026, 10)).Quotas.Select(q => q.Target));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(ErrorCodes.QuotaInvalid, await wrong.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        var stored = await SendAsync(HttpMethod.Get, "/api/v1/users/me/quotas", person);
        Assert.Equal(new QuotaSettings(60, 20, 20), await stored.Content.ReadFromJsonAsync<QuotaSettings>());
    }

    // A13
    [Fact]
    public async Task Quota_actual_leaves_out_pass_through_positions()
    {
        var person = await SignUpAsync();
        await RentAsync(person, 1500.00m);
        await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Weiterleitung", 200.00m, 1, January1, null, person.Groceries, PotKeys.Needs, PassThrough: true));
        var october = await PlanAsync(person, 2026, 10);
        await BookAsync(person, Income(person, 3000.00m));
        await BookAsync(person, Expense(person, 1500.00m, Position(october, "Miete").Id, category: person.Rent));
        await BookAsync(person, Expense(person, 200.00m, Position(october, "Weiterleitung").Id));

        var quotas = (await PlanAsync(person, 2026, 10)).Quotas;

        Assert.Equal(50.0m, quotas.Single(q => q.Pot == PotKeys.Needs).Actual);
        Assert.Equal(50, quotas.Single(q => q.Pot == PotKeys.Needs).Target);
        Assert.Equal(0m, quotas.Single(q => q.Pot == PotKeys.Wants).Actual);
    }

    // A14
    [Fact]
    public async Task The_carry_over_books_a_pair_that_cancels_out_and_balances_each_account_once()
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));
        await BookAsync(person, Expense(person, 2770.00m));
        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal(230.00m, november.CarryOver.Suggested);
        Assert.False(november.CarryOver.Exists);
        // Gerechnet am 31. Oktober: 1000 + 3000 - 2770.
        var computed = november.CarryOver.Balances.Single(b => b.AccountId == person.Giro).Balance;
        Assert.Equal(1230.00m, computed);

        var created = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person,
            new CreateCarryOverRequest(230.00m, [new RealBalance(person.Giro, computed - 10.00m)]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var income = await CarryOverBookingAsync(person, "Übertrag aus Oktober");
        Assert.Equal(BookingKinds.Income, income.Kind);
        Assert.Equal("Übertrag aus Oktober", income.Note);
        Assert.Equal(230.00m, income.Amount);
        // Beide Buchungen stehen am letzten Tag des alten Monats, die Einnahme zählt über den Planmonat im November.
        Assert.Equal(new DateOnly(2026, 10, 31), income.Date);
        Assert.Equal((2026, 11), (income.PlanYear, income.PlanMonth));
        var october = await BookingsInAsync(person, 2026, 10);
        var outgoing = await CarryOverBookingAsync(person, "Übertrag nach November");
        Assert.Equal((BookingKinds.Expense, 230.00m, new DateOnly(2026, 10, 31)), (outgoing.Kind, outgoing.Amount, outgoing.Date));
        Assert.Equal((2026, 10), (outgoing.PlanYear, outgoing.PlanMonth));
        var balancing = october.Single(b => b.Amount == 10.00m);
        Assert.Equal((BookingKinds.Expense, new DateOnly(2026, 10, 31), person.Giro), (balancing.Kind, balancing.Date, balancing.AccountId));
        // Das Paar hebt sich auf; nur der Ausgleich verändert den Stand.
        Assert.Equal(computed - 10.00m, await BalanceOnAsync(person.Giro, new DateOnly(2026, 11, 30)));
        Assert.True((await PlanAsync(person, 2026, 11)).CarryOver.Exists);
        // Der Übertrag zählt nicht als Einnahme der Quoten.
        Assert.All((await PlanAsync(person, 2026, 11)).Quotas, q => Assert.Equal(0m, q.Actual));

        var second = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, new CreateCarryOverRequest(230.00m));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.CarryOverExists, await second.ReadCodeAsync());
    }

    // A20
    [Fact]
    public async Task A_negative_rest_is_carried_with_the_pair_swapped_and_a_rest_of_zero_only_balances_the_accounts()
    {
        var person = await SignUpAsync();
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 2150.00m, person.Groceries);
        await BookAsync(person, Income(person, 2000.00m));
        await BookAsync(person, Expense(person, 2150.00m, groceries.Id));
        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal(-150.00m, november.CarryOver.Suggested);
        var computed = november.CarryOver.Balances.Single(b => b.AccountId == person.Giro).Balance;

        var created = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, new CreateCarryOverRequest(-150.00m));

        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var incoming = await CarryOverBookingAsync(person, "Übertrag aus Oktober");
        Assert.Equal((BookingKinds.Expense, "Übertrag aus Oktober", 150.00m), (incoming.Kind, incoming.Note, incoming.Amount));
        Assert.Equal((new DateOnly(2026, 10, 31), 2026, 11), (incoming.Date, incoming.PlanYear, incoming.PlanMonth));
        var outgoing = await CarryOverBookingAsync(person, "Übertrag nach November");
        Assert.Equal((BookingKinds.Income, 150.00m), (outgoing.Kind, outgoing.Amount));
        Assert.Equal((new DateOnly(2026, 10, 31), 2026, 10), (outgoing.Date, outgoing.PlanYear, outgoing.PlanMonth));
        Assert.Equal(computed, await BalanceOnAsync(person.Giro, new DateOnly(2026, 11, 30)));
        var after = await PlanAsync(person, 2026, 11);
        Assert.True(after.CarryOver.Exists);
        Assert.Equal(-150.00m, after.CarryOver.Amount);
        // Die Einnahme "Übertrag nach November" ist kein Geld, das im Oktober verdient wurde: 2150 von 2000.
        Assert.Equal(107.5m, (await PlanAsync(person, 2026, 10)).Quotas.Single(q => q.Pot == PotKeys.Needs).Actual);

        var december = await PlanAsync(person, 2026, 12);
        var computedDecember = december.CarryOver.Balances.Single(b => b.AccountId == person.Giro).Balance;
        var zero = await SendAsync(HttpMethod.Post, $"{Plans}/2026/12/carry-over", person,
            new CreateCarryOverRequest(0m, [new RealBalance(person.Giro, computedDecember - 5.00m)]));

        Assert.True(zero.StatusCode == HttpStatusCode.Created, await zero.Content.ReadAsStringAsync());
        Assert.Empty(await BookingsInAsync(person, 2026, 12));
        var balancing = (await BookingsInAsync(person, 2026, 11)).Single(b => b.Amount == 5.00m);
        Assert.Equal((BookingKinds.Expense, new DateOnly(2026, 11, 30)), (balancing.Kind, balancing.Date));
        Assert.False((await PlanAsync(person, 2026, 12)).CarryOver.Exists);
    }

    // Toms Entscheidung vom 1.11.: Der Ausgleich nach unten zählt in Wünsche, nach oben bleibt er eine Einnahme.
    [Fact]
    public async Task A_balancing_expense_counts_in_wants_a_balancing_income_stays_income_and_the_balances_match_what_was_entered()
    {
        var person = await SignUpAsync();
        var cash = await AccountAsync(person, "Bar", 100.00m);
        await BookAsync(person, Income(person, 3000.00m));
        await BookAsync(person, Expense(person, 2770.00m));
        var balances = (await PlanAsync(person, 2026, 11)).CarryOver.Balances;
        var realGiro = balances.Single(b => b.AccountId == person.Giro).Balance - 10.00m;
        var realCash = balances.Single(b => b.AccountId == cash).Balance + 25.00m;

        var created = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person,
            new CreateCarryOverRequest(230.00m, [new RealBalance(person.Giro, realGiro), new RealBalance(cash, realCash)]));

        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var october = await BookingsInAsync(person, 2026, 10);
        var down = october.Single(b => b.Note == "Ausgleich" && b.AccountId == person.Giro);
        Assert.Equal((BookingKinds.Expense, 10.00m, PotKeys.Wants), (down.Kind, down.Amount, Assert.Single(down.Lines).Pot));
        var up = october.Single(b => b.Note == "Ausgleich" && b.AccountId == cash);
        Assert.Equal((BookingKinds.Income, 25.00m, PotKeys.Income), (up.Kind, up.Amount, Assert.Single(up.Lines).Pot));
        // Das Paar des Übertrags bleibt, wie es war: Der Rest kommt im November als Einnahme an.
        Assert.Equal(PotKeys.Needs, Assert.Single((await CarryOverBookingAsync(person, "Übertrag nach November")).Lines).Pot);
        var arriving = await CarryOverBookingAsync(person, "Übertrag aus Oktober");
        Assert.Equal((BookingKinds.Income, 230.00m, PotKeys.Income), (arriving.Kind, arriving.Amount, Assert.Single(arriving.Lines).Pot));

        Assert.Equal(realGiro, await BalanceOnAsync(person.Giro, new DateOnly(2026, 11, 30)));
        Assert.Equal(realCash, await BalanceOnAsync(cash, new DateOnly(2026, 11, 30)));
        // Gebucht im Topf nach Spec 72, R3; in der Quote zählen nur Posten (R5).
        var plan = await PlanAsync(person, 2026, 10);
        Assert.Equal(10.00m, plan.Overview!.Pots.Single(p => p.Pot == PotKeys.Wants).Booked);
        Assert.Equal(0m, plan.Quotas.Single(q => q.Pot == PotKeys.Wants).Actual);
    }

    /// <summary>Ein Posten aus einem Vertrag mit Zielkonto; nur der Abgleich legt so einen an.</summary>
    private async Task<(PlanPositionResponse Position, Guid Target)> PositionWithTargetAsync(Person person)
    {
        var target = await AccountAsync(person, "Bar", 0m);
        await CommitmentAsync(person, new CreateCommitmentRequest(
            CommitmentTypes.Contract, "Rücklage", 100.00m, 1, January1, null, person.Rent, PotKeys.Needs, CounterAccountId: target));
        return ((await PlanAsync(person, 2026, 10)).Positions.Single(p => p.CounterAccountId == target), target);
    }

    [Fact]
    public async Task A_position_with_a_target_account_does_not_become_income()
    {
        var person = await SignUpAsync();
        var (position, _) = await PositionWithTargetAsync(person);

        var refused = await SendAsync(HttpMethod.Patch, $"{Plans}/2026/10/positions/{position.Id}", person,
            new UpdatePositionRequest(Pot: PotKeys.Income, CategoryId: person.Salary));

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.PositionTargetInvalid), (refused.StatusCode, await refused.ReadCodeAsync()));
        Assert.Equal(PotKeys.Needs, (await PlanAsync(person, 2026, 10)).Positions.Single(p => p.Id == position.Id).Pot);
    }

    // Vor dieser Regel ließ sich so ein Posten speichern; er bleibt bedienbar und sagt beim Abhaken, was zu tun ist.
    [Fact]
    public async Task A_stored_income_position_with_a_target_account_explains_the_refused_tick_and_can_be_repaired()
    {
        var person = await SignUpAsync();
        var (position, _) = await PositionWithTargetAsync(person);
        await fixture.Factory.WithDbAsync(async db =>
        {
            var stored = await db.PlanPositions.SingleAsync(p => p.Id == position.Id);
            stored.Pot = Duofy.Domain.Pot.Income;
            stored.CategoryId = person.Salary;
            return await db.SaveChangesAsync();
        });
        var path = $"{Plans}/2026/10/positions/{position.Id}";

        var tick = await SendAsync(HttpMethod.Post, $"{path}/tick", person);

        Assert.Equal((HttpStatusCode.BadRequest, ErrorCodes.PositionIncomeWithTarget), (tick.StatusCode, await tick.ReadCodeAsync()));
        Assert.Empty(await BookingsInAsync(person, 2026, 10));
        var edited = await SendAsync(HttpMethod.Patch, path, person, new UpdatePositionRequest(Amount: 120.00m));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var repaired = await SendAsync(HttpMethod.Patch, path, person, new UpdatePositionRequest(Pot: PotKeys.Needs, CategoryId: person.Rent));
        Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
        var ticked = await SendAsync(HttpMethod.Post, $"{path}/tick", person);
        Assert.True(ticked.StatusCode == HttpStatusCode.OK, await ticked.Content.ReadAsStringAsync());
    }

    // A21
    [Fact]
    public async Task A_carry_over_naming_an_account_twice_is_refused_and_books_nothing()
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));

        var refused = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person,
            new CreateCarryOverRequest(100.00m, [new RealBalance(person.Giro, 3990.00m), new RealBalance(person.Giro, 3980.00m)]));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(ErrorCodes.CarryOverBalanceDuplicate, await refused.ReadCodeAsync());
        Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.Empty(await BookingsInAsync(person, 2026, 11));
        Assert.False((await PlanAsync(person, 2026, 11)).CarryOver.Exists);
    }

    // Mangel S14: Ein Stand ohne Betrag (Feld vergessen oder falsch benannt) war 0 und glich das ganze Konto aus.
    [Theory]
    [InlineData("accountId only")]
    [InlineData("misspelled field")]
    public async Task A_real_balance_without_its_amount_is_refused_and_books_nothing(string shape)
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));
        object body = shape == "accountId only"
            ? new { amount = 100.00m, balances = new[] { new { accountId = person.Giro } } }
            : new { amount = 100.00m, balances = new[] { new { accountId = person.Giro, realBalance = 3990.00m } } };

        var refused = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, body);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(ErrorCodes.AmountInvalid, await refused.ReadCodeAsync());
        Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.Empty(await BookingsInAsync(person, 2026, 11));
        Assert.False((await PlanAsync(person, 2026, 11)).CarryOver.Exists);
    }

    [Fact]
    public async Task Removing_the_carry_over_deletes_both_bookings_of_the_pair()
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));
        await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, new CreateCarryOverRequest(100.00m));

        var removed = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/11/carry-over", person);
        var again = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/11/carry-over", person);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await BookingsInAsync(person, 2026, 11));
        Assert.Single(await BookingsInAsync(person, 2026, 10));
        Assert.False((await PlanAsync(person, 2026, 11)).CarryOver.Exists);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    // A18
    [Fact]
    public async Task The_two_bookings_of_a_carry_over_change_only_through_the_plan()
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));
        await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, new CreateCarryOverRequest(230.00m));
        var incoming = await CarryOverBookingAsync(person, "Übertrag aus Oktober");
        var outgoing = await CarryOverBookingAsync(person, "Übertrag nach November");

        foreach (var booking in new[] { incoming, outgoing })
        {
            var line = Assert.Single(booking.Lines);
            var put = await SendAsync(HttpMethod.Put, $"{Bookings}/{booking.Id}", person,
                new SaveBookingRequest(booking.Kind, booking.AccountId, null, booking.Date, 1.00m, booking.Note,
                    [new SaveBookingLine(1.00m, line.Pot, line.CategoryId)], booking.PlanYear, booking.PlanMonth));
            var delete = await SendAsync(HttpMethod.Delete, $"{Bookings}/{booking.Id}", person);

            Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
            Assert.Equal(ErrorCodes.CarryOverBooking, await put.ReadCodeAsync());
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            Assert.Equal(ErrorCodes.CarryOverBooking, await delete.ReadCodeAsync());
        }
        Assert.Equal(230.00m, (await CarryOverBookingAsync(person, "Übertrag aus Oktober")).Amount);
        Assert.Equal(230.00m, (await CarryOverBookingAsync(person, "Übertrag nach November")).Amount);

        var removed = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/11/carry-over", person);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await BookingsInAsync(person, 2026, 11));
        Assert.DoesNotContain(await BookingsInAsync(person, 2026, 10), b => b.Id == outgoing.Id || b.Id == incoming.Id);
    }

    // A15
    [Fact]
    public async Task A_position_with_bookings_cannot_be_deleted_and_deleting_one_keeps_the_contract()
    {
        var person = await SignUpAsync();
        var rent = await RentAsync(person);
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 600.00m, person.Groceries);
        await BookAsync(person, Expense(person, 20.00m, groceries.Id));
        var rentPosition = Position(await PlanAsync(person, 2026, 10), "Miete");

        var refused = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/10/positions/{groceries.Id}", person);
        var deleted = await SendAsync(HttpMethod.Delete, $"{Plans}/2026/10/positions/{rentPosition.Id}", person);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(ErrorCodes.PositionHasBookings, await refused.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var commitments = await (await SendAsync(HttpMethod.Get, "/api/v1/commitments", person))
            .Content.ReadFromJsonAsync<List<CommitmentResponse>>();
        Assert.Contains(commitments!, c => c.Id == rent.Id);
        Assert.DoesNotContain((await PlanAsync(person, 2026, 10)).Positions, p => p.Id == rentPosition.Id);
    }

    // A22
    [Fact]
    public async Task Two_ticks_at_the_same_time_book_the_position_once()
    {
        var person = await SignUpAsync();
        // Mehrere Runden, damit ein Wettlauf ohne Sperre sicher auffällt.
        for (var round = 1; round <= 5; round++)
        {
            var position = await OneOffAsync(person, 2026, 10, $"Posten {round}", 10.00m, person.Groceries);
            var path = $"{Plans}/2026/10/positions/{position.Id}/tick";

            var ticks = await Task.WhenAll(
                SendAsync(HttpMethod.Post, path, person, new TickPositionRequest()),
                SendAsync(HttpMethod.Post, path, person, new TickPositionRequest()));

            Assert.All(ticks, tick => Assert.Equal(HttpStatusCode.OK, tick.StatusCode));
            var booking = Assert.Single(await BookingsInAsync(person, 2026, 10), b => b.Lines.Any(l => l.PositionId == position.Id));
            Assert.Equal(booking.Id, Position(await PlanAsync(person, 2026, 10), position.Label).TickBookingId);
        }
    }

    // A22
    [Fact]
    public async Task Two_carry_overs_of_one_month_at_the_same_time_book_one_pair()
    {
        var person = await SignUpAsync();
        await BookAsync(person, Income(person, 3000.00m));
        (int Year, int Month)[] months = [(2026, 11), (2026, 12), (2027, 1), (2027, 2), (2027, 3)];

        foreach (var (year, month) in months)
        {
            var path = $"{Plans}/{year}/{month}/carry-over";

            var answers = await Task.WhenAll(
                SendAsync(HttpMethod.Post, path, person, new CreateCarryOverRequest(100.00m)),
                SendAsync(HttpMethod.Post, path, person, new CreateCarryOverRequest(100.00m)));

            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], answers.Select(a => a.StatusCode).Order());
            Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(
                b => b.AccountId == person.Giro && b.PlanYear == year && b.PlanMonth == month && b.Note!.StartsWith("Übertrag aus"))));
        }
    }

    // A23
    [Fact]
    public async Task The_pot_kind_of_a_position_with_bookings_cannot_change()
    {
        var person = await SignUpAsync();
        var groceries = await OneOffAsync(person, 2026, 10, "Lebensmittel", 600.00m, person.Groceries);
        var salary = await OneOffAsync(person, 2026, 10, "Gehalt", 2400.00m, person.Salary, PotKeys.Income);
        await BookAsync(person, Expense(person, 20.00m, groceries.Id));
        await BookAsync(person, Income(person, 2400.00m) with
        {
            Lines = [new SaveBookingLine(2400.00m, PotKeys.Income, person.Salary, salary.Id)],
        });

        var toIncome = await SendAsync(HttpMethod.Patch, $"{Plans}/2026/10/positions/{groceries.Id}", person,
            new UpdatePositionRequest(CategoryId: person.Salary, Pot: PotKeys.Income));
        var toNeeds = await SendAsync(HttpMethod.Patch, $"{Plans}/2026/10/positions/{salary.Id}", person,
            new UpdatePositionRequest(CategoryId: person.Groceries, Pot: PotKeys.Needs));
        var toWants = await SendAsync(HttpMethod.Patch, $"{Plans}/2026/10/positions/{groceries.Id}", person,
            new UpdatePositionRequest(Pot: PotKeys.Wants));

        foreach (var refused in new[] { toIncome, toNeeds })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(ErrorCodes.PositionHasBookings, await refused.ReadCodeAsync());
        }
        Assert.Equal(HttpStatusCode.OK, toWants.StatusCode);
        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal(PotKeys.Wants, Position(october, "Lebensmittel").Pot);
        Assert.Equal(PotKeys.Income, Position(october, "Gehalt").Pot);
    }

    // A16
    [Fact]
    public async Task Another_persons_positions_look_unknown_and_stay_unchanged()
    {
        var anna = await SignUpAsync();
        await RentAsync(anna);
        var position = Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);
        var ben = await SignUpAsync();
        var path = $"{Plans}/2026/11/positions/{position.Id}";

        var patch = await SendAsync(HttpMethod.Patch, path, ben, new UpdatePositionRequest(Amount: 1m));
        var delete = await SendAsync(HttpMethod.Delete, path, ben);
        var tick = await SendAsync(HttpMethod.Post, path + "/tick", ben, new TickPositionRequest());
        var line = await SendAsync(HttpMethod.Post, Bookings, ben,
            Expense(ben, 20.00m, position.Id, new DateOnly(2026, 10, 24)) with { PlanYear = 2026, PlanMonth = 11 });

        foreach (var response in new[] { patch, delete, tick })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ErrorCodes.PositionNotFound, await response.ReadCodeAsync());
        }
        Assert.Equal(HttpStatusCode.NotFound, line.StatusCode);
        Assert.Empty((await PlanAsync(ben, 2026, 11)).Positions);
        var unchanged = Assert.Single((await PlanAsync(anna, 2026, 11)).Positions);
        Assert.Equal((890.00m, (DateTimeOffset?)null), (unchanged.AmountPlanned, unchanged.DoneAt));
        var quotas = await SendAsync(HttpMethod.Get, "/api/v1/users/me/quotas", ben);
        Assert.Equal(new QuotaSettings(50, 30, 20), await quotas.Content.ReadFromJsonAsync<QuotaSettings>());
    }

    /// <summary>Der Stand eines Kontos an einem Tag, aus der Datenbank gerechnet wie in der API.</summary>
    private Task<decimal> BalanceOnAsync(Guid accountId, DateOnly day) =>
        fixture.Factory.WithDbAsync(async db =>
        {
            var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId);
            account.Bookings = await db.Bookings.AsNoTracking().Include(b => b.Lines)
                .Where(b => b.AccountId == accountId || b.CounterAccountId == accountId)
                .ToListAsync();
            return account.Balance(day);
        });

    // Spec 24, R2/R4: die Blöcke und die Töpfe kommen aus dem Plan der API, nichts wird auf der Seite gerechnet.
    [Fact]
    public async Task The_month_carries_income_fixed_free_and_the_pot_totals()
    {
        var person = await SignUpAsync();
        await OneOffAsync(person, 2026, 10, "Gehalt", 3000.00m, person.Salary, PotKeys.Income);
        var rent = await OneOffAsync(person, 2026, 10, "Miete", 1000.00m, person.Rent);
        await OneOffAsync(person, 2026, 10, "Sparplan", 200.00m, person.Savings, PotKeys.Savings);
        await BookAsync(person, Expense(person, 1200.00m, rent.Id, new DateOnly(2026, 10, 3)));

        var overview = (await PlanAsync(person, 2026, 10)).Overview!;

        Assert.Equal(3000.00m, overview.Income);
        Assert.Equal(1200.00m, overview.Fixed);
        // Die Miete ist mit 1200 überbucht und zählt deshalb mit 1200 statt der geplanten 1000 (Spec 93, R7);
        // Einnahmen und Festes zeigen weiter die geplanten Beträge.
        Assert.Equal(1600.00m, overview.Free);
        Assert.Equal(new PlanPotResponse(PotKeys.Needs, 1000.00m, 1200.00m), overview.Pots.Single(p => p.Pot == PotKeys.Needs));
        Assert.Equal(new PlanPotResponse(PotKeys.Wants, 0m, 0m), overview.Pots.Single(p => p.Pot == PotKeys.Wants));
        Assert.Equal(200.00m, overview.Pots.Single(p => p.Pot == PotKeys.Savings).Planned);
    }

    // Spec 72, A2: Ein Posten ist freiwillig, der Topf nicht; was nie geplant war, zeigt das Lückendiagramm.
    [Fact]
    public async Task A_booking_without_a_position_counts_as_booked_in_its_pot()
    {
        var person = await SignUpAsync();

        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, person.Giro, null, October5, 40.00m, null,
            [new SaveBookingLine(40.00m, PotKeys.Wants, person.Gifts)]));

        var overview = (await PlanAsync(person, 2026, 10)).Overview!;
        Assert.Equal(new PlanPotResponse(PotKeys.Wants, 0m, 40.00m), overview.Pots.Single(p => p.Pot == PotKeys.Wants));
    }

    // Spec 72, A8 (R5): Die Quote zählt nur Posten, die Ausgabe ohne Posten bleibt draußen.
    [Fact]
    public async Task A_booking_without_a_position_does_not_count_in_the_actual_of_its_quota()
    {
        var person = await SignUpAsync();
        var wishes = await OneOffAsync(person, 2026, 10, "Wünsche", 50.00m, person.Gifts, PotKeys.Wants);
        await BookAsync(person, Income(person, 1000.00m));
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, person.Giro, null, October5, 50.00m, null,
            [new SaveBookingLine(50.00m, PotKeys.Wants, person.Gifts, wishes.Id)]));
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, person.Giro, null, October5, 100.00m, null,
            [new SaveBookingLine(100.00m, PotKeys.Wants, person.Gifts)]));

        var quotas = (await PlanAsync(person, 2026, 10)).Quotas;

        Assert.Equal(5.0m, quotas.Single(q => q.Pot == PotKeys.Wants).Actual);
    }

    // Spec 72, A3: Zeilen auf einem Posten zählen über den Posten, nichts doppelt.
    [Fact]
    public async Task A_pot_adds_what_is_booked_on_its_positions_and_what_is_unplanned()
    {
        var person = await SignUpAsync();
        var rent = await OneOffAsync(person, 2026, 10, "Miete", 1000.00m, person.Rent);
        await BookAsync(person, Expense(person, 100.00m, rent.Id));
        await BookAsync(person, Expense(person, 40.00m));

        var overview = (await PlanAsync(person, 2026, 10)).Overview!;

        Assert.Equal(new PlanPotResponse(PotKeys.Needs, 1000.00m, 140.00m), overview.Pots.Single(p => p.Pot == PotKeys.Needs));
    }

    // Spec 72, A11 (R9) und Spec 112, A1/A2/A6: Quelle und Ziel einer Umbuchung verschieben den Betrag im gebuchten Topf und im Ist der Quote; Plan und Posten bleiben gleich.
    [Fact]
    public async Task A_transfer_between_pots_moves_booked_sums_and_quota_actuals_but_not_the_plan()
    {
        var person = await SignUpAsync();
        var savingsAccount = await AccountAsync(person, "Sparkonto", 500m);
        await OneOffAsync(person, 2026, 10, "Wünsche", 300.00m, person.Gifts, PotKeys.Wants);
        await OneOffAsync(person, 2026, 10, "Sparen", 200.00m, person.Gifts, PotKeys.Savings);
        await BookAsync(person, Income(person, 1000.00m));
        var before = await PlanAsync(person, 2026, 10);

        await BookAsync(person, new SaveBookingRequest(BookingKinds.Transfer, savingsAccount, person.Giro, October5, 100.00m, null,
            [new SaveBookingLine(100.00m, PotKeys.Savings, null)], TargetPot: PotKeys.Wants));

        var after = await PlanAsync(person, 2026, 10);
        PlanPotResponse Pot(PlanResponse plan, string key) => plan.Overview!.Pots.Single(p => p.Pot == key);
        Assert.Equal(Pot(before, PotKeys.Savings) with { Booked = Pot(before, PotKeys.Savings).Booked - 100.00m }, Pot(after, PotKeys.Savings));
        Assert.Equal(Pot(before, PotKeys.Wants) with { Booked = Pot(before, PotKeys.Wants).Booked + 100.00m }, Pot(after, PotKeys.Wants));
        Assert.Equal(Pot(before, PotKeys.Needs), Pot(after, PotKeys.Needs));
        Assert.Equal(before.Quotas.Single(q => q.Pot == PotKeys.Savings).Actual - 10.0m, after.Quotas.Single(q => q.Pot == PotKeys.Savings).Actual);
        Assert.Equal(before.Quotas.Single(q => q.Pot == PotKeys.Wants).Actual + 10.0m, after.Quotas.Single(q => q.Pot == PotKeys.Wants).Actual);
        Assert.Equal(before.Positions.Select(p => (p.Id, p.Booked)), after.Positions.Select(p => (p.Id, p.Booked)));
    }

    // Spec 72, A4; seit Spec 112 mit Quelle und Ziel im selben Topf (verschiebt nichts)
    [Fact]
    public async Task Transfers_and_the_carry_over_count_as_booked_in_no_pot()
    {
        var person = await SignUpAsync();
        var cash = await AccountAsync(person, "Bar", 0m);
        await BookAsync(person, Income(person, 2000.00m));
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Transfer, person.Giro, cash, October5, 100.00m, null,
            [new SaveBookingLine(100.00m, PotKeys.Savings, null)], TargetPot: PotKeys.Savings));
        await PlanAsync(person, 2026, 11);

        // Der Rest geht als Paar in den November: die Ausgabe "Übertrag nach November" liegt im Planmonat Oktober.
        var created = await SendAsync(HttpMethod.Post, $"{Plans}/2026/11/carry-over", person, new CreateCarryOverRequest(2000.00m));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        foreach (var (year, month) in new[] { (2026, 10), (2026, 11) })
        {
            var overview = (await PlanAsync(person, year, month)).Overview!;
            Assert.All(overview.Pots, pot => Assert.Equal(0m, pot.Booked));
        }
    }

    // Spec 24, R8/A9: eine Buchung vom 20. November kann dem Dezember zugeordnet sein.
    [Fact]
    public async Task The_month_lists_the_bookings_of_its_plan_month_newest_first_even_when_their_date_is_in_another_month()
    {
        var person = await SignUpAsync();
        var october20 = new DateOnly(2026, 10, 20);
        var inDecember = new SaveBookingRequest(BookingKinds.Expense, person.Giro, null, october20, 45.00m, null,
            [new SaveBookingLine(45.00m, PotKeys.Needs, person.Groceries)], 2026, 11);
        var assigned = await BookAsync(person, inDecember);
        var newer = await BookAsync(person, Expense(person, 10.00m, null, new DateOnly(2026, 10, 22)) with { PlanYear = 2026, PlanMonth = 11 });
        var inOctober = await BookAsync(person, Expense(person, 5.00m, null, new DateOnly(2026, 10, 2)));

        var november = (await PlanAsync(person, 2026, 11)).Overview!.Bookings;
        var october = (await PlanAsync(person, 2026, 10)).Overview!.Bookings;

        Assert.Equal([newer.Id, assigned.Id], november.Select(b => b.Id));
        Assert.Equal(october20, november[1].Date);
        Assert.Equal([inOctober.Id], october.Select(b => b.Id));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Spec 51, A9: Tom: "bei GEZ, dass eine 2. Buchung zb zu Mahnkosten zählt, aber immer noch Teil des Postens ist."
    [Fact]
    public async Task A_second_booking_with_another_category_counts_on_a_ticked_position()
    {
        var person = await SignUpAsync();
        var gez = await OneOffAsync(person, 2026, 10, "GEZ", 18.36m, person.Rent);
        var tick = await SendAsync(HttpMethod.Post, $"{Plans}/2026/10/positions/{gez.Id}/tick", person, new TickPositionRequest());
        Assert.Equal(HttpStatusCode.OK, tick.StatusCode);

        await BookAsync(person, Expense(person, 5.00m, gez.Id, category: person.Gifts));

        var position = Assert.Single((await PlanAsync(person, 2026, 10)).Positions);
        Assert.Equal(23.36m, position.Booked);
        Assert.NotNull(position.DoneAt);
    }
}
