using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;
using static Duofy.Api.Tests.ApiDriver;

namespace Duofy.Api.Tests;

/// <summary>
/// Buchungen über die API mit echter Datenbank: anlegen, ändern, löschen, Monatsliste, und die
/// Kontostände folgen (Spec 59, R1, R5, R7, R10, R11, R13). Die einzelnen Regeln der Prüfung
/// stehen in <see cref="BookingRulesTests"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public class BookingTests
{
    private const string Path = "/api/v1/bookings";

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October5 = new(2026, 10, 5);

    private readonly HttpClient client;

    public BookingTests(ApiFixture fixture)
    {
        client = fixture.Factory.CreateClient();
    }

    /// <summary>Eine angemeldete Person mit ihren Kategorien, so wie die Tests sie brauchen.</summary>
    private sealed record Person(string Token, Guid Groceries, Guid PersonalCare, Guid Salary);

    private async Task<Person> SignUpAsync()
    {
        var (_, tokens) = await client.SignUpAsync();
        var categories = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/categories", tokens.AccessToken));
        var all = (await categories.Content.ReadFromJsonAsync<List<CategoryResponse>>())!;
        Guid Id(string key) => all.Single(c => c.Key == key).Id;
        return new Person(tokens.AccessToken, Id("household.groceries"), Id("household.personal_care"), Id("income.earned"));
    }

    private async Task<Guid> AccountAsync(Person person, string type, decimal openingBalance, string name = "Konto")
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/api/v1/accounts", person.Token,
            new CreateAccountRequest(name, type, openingBalance, October1)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    private async Task<decimal> BalanceAsync(Person person, Guid accountId)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/accounts", person.Token));
        var accounts = (await response.Content.ReadFromJsonAsync<List<AccountResponse>>())!;
        return accounts.Single(a => a.Id == accountId).Balance;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Person person, object? body = null) =>
        client.SendAsync(Authorized(method, path, person.Token, body));

    private async Task<BookingResponse> CreateAsync(Person person, SaveBookingRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Path, person, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }

    private async Task<BookingMonthResponse> MonthAsync(Person person, int year, int month, Guid? accountId = null)
    {
        var query = $"?year={year}&month={month}" + (accountId is { } id ? $"&accountId={id}" : "");
        var response = await SendAsync(HttpMethod.Get, Path + query, person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingMonthResponse>())!;
    }

    private static SaveBookingRequest Expense(Guid account, decimal amount, Guid category, DateOnly? date = null, string pot = PotKeys.Needs) =>
        new(BookingKinds.Expense, account, null, date ?? October5, amount, null, [new SaveBookingLine(amount, pot, category)]);

    private static SaveBookingRequest Income(Guid account, decimal amount, Guid category) =>
        new(BookingKinds.Income, account, null, October5, amount, null, [new SaveBookingLine(amount, PotKeys.Income, category)]);

    private static SaveBookingRequest Transfer(Guid from, Guid to, decimal amount, string? pot = null) =>
        new(BookingKinds.Transfer, from, to, October5, amount, null, pot is null ? [] : [new SaveBookingLine(amount, pot, null)]);

    [Fact]
    public async Task Bookings_need_a_signed_in_person()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path + "?year=2026&month=10")).StatusCode);
    }

    // A1
    [Fact]
    public async Task An_expense_lowers_the_balance()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);

        var response = await SendAsync(HttpMethod.Post, Path, person,
            Expense(giro, 42.10m, person.Groceries, new DateOnly(2026, 10, 21)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(BookingKinds.Expense, saved.Kind);
        Assert.Equal(42.10m, saved.Amount);
        Assert.Equal(957.90m, await BalanceAsync(person, giro));
    }

    // A2
    [Fact]
    public async Task An_income_raises_the_balance_and_amounts_of_zero_or_below_are_refused()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);

        await CreateAsync(person, Income(giro, 2450.00m, person.Salary));
        var zero = await SendAsync(HttpMethod.Post, Path, person, Expense(giro, 0m, person.Groceries));
        var negative = await SendAsync(HttpMethod.Post, Path, person, Expense(giro, -5m, person.Groceries));

        Assert.Equal(3450.00m, await BalanceAsync(person, giro));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    // A8
    [Fact]
    public async Task A_transfer_moves_money_between_two_accounts()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m, "Giro");
        var cash = await AccountAsync(person, AccountTypes.Cash, 0.00m, "Bargeld");

        var saved = await CreateAsync(person, Transfer(giro, cash, 100.00m));

        Assert.Equal(BookingKinds.Transfer, saved.Kind);
        Assert.Equal(900.00m, await BalanceAsync(person, giro));
        Assert.Equal(100.00m, await BalanceAsync(person, cash));
    }

    // A10
    [Fact]
    public async Task A_savings_transfer_is_stored_with_pot_savings()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m, "Giro");
        var savings = await AccountAsync(person, AccountTypes.Savings, 0.00m, "Spar");

        var saved = await CreateAsync(person, Transfer(giro, savings, 50.00m, PotKeys.Savings));

        var stored = Assert.Single((await MonthAsync(person, 2026, 10)).Bookings, b => b.Id == saved.Id);
        Assert.Equal(PotKeys.Savings, Assert.Single(stored.Lines).Pot);
        Assert.Null(stored.Lines[0].CategoryId);
    }

    // A19
    [Fact]
    public async Task A_split_expense_keeps_its_lines_and_uses_the_total_for_the_balance()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        var request = new SaveBookingRequest(BookingKinds.Expense, giro, null, October5, 80.00m, "Drogeriemarkt",
            [new SaveBookingLine(60.00m, PotKeys.Needs, person.Groceries), new SaveBookingLine(20.00m, PotKeys.Needs, person.PersonalCare)]);

        var response = await SendAsync(HttpMethod.Post, Path, person, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(2, saved.Lines.Count);
        Assert.Equal([60.00m, 20.00m], saved.Lines.Select(l => l.Amount));
        Assert.Equal([person.Groceries, person.PersonalCare], saved.Lines.Select(l => l.CategoryId!.Value));
        Assert.Equal("Drogeriemarkt", saved.Note);
        Assert.Equal(920.00m, await BalanceAsync(person, giro));
    }

    [Fact]
    public async Task A_rule_violation_answers_400_with_its_code_and_saves_nothing()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);

        var response = await SendAsync(HttpMethod.Post, Path, person, Expense(giro, 42.10m, person.Groceries, October1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.DateBeforeOpening, await response.ReadCodeAsync());
        Assert.Empty((await MonthAsync(person, 2026, 10)).Bookings);
    }

    // A12
    [Fact]
    public async Task Changing_the_amount_moves_the_balance_and_deleting_restores_it()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        var saved = await CreateAsync(person, Expense(giro, 42.10m, person.Groceries));

        var put = await SendAsync(HttpMethod.Put, $"{Path}/{saved.Id}", person, Expense(giro, 50.00m, person.Groceries));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(saved.Id, (await put.Content.ReadFromJsonAsync<BookingResponse>())!.Id);
        Assert.Equal(950.00m, await BalanceAsync(person, giro));

        var delete = await SendAsync(HttpMethod.Delete, $"{Path}/{saved.Id}", person);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(1000.00m, await BalanceAsync(person, giro));
        Assert.Empty((await MonthAsync(person, 2026, 10)).Bookings);
    }

    [Fact]
    public async Task A_change_can_split_a_booking_and_follows_the_same_rules()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        var saved = await CreateAsync(person, Expense(giro, 80.00m, person.Groceries));
        var split = new SaveBookingRequest(BookingKinds.Expense, giro, null, October5, 80.00m, null,
            [new SaveBookingLine(60.00m, PotKeys.Needs, person.Groceries), new SaveBookingLine(20.00m, PotKeys.Wants, person.PersonalCare)]);

        var ok = await SendAsync(HttpMethod.Put, $"{Path}/{saved.Id}", person, split);
        var mismatch = await SendAsync(HttpMethod.Put, $"{Path}/{saved.Id}", person, split with { Amount = 90.00m });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2, (await ok.Content.ReadFromJsonAsync<BookingResponse>())!.Lines.Count);
        Assert.Equal(ErrorCodes.LinesSumMismatch, await mismatch.ReadCodeAsync());
        Assert.Equal(2, Assert.Single((await MonthAsync(person, 2026, 10)).Bookings).Lines.Count);
    }

    // A14 und A15 (API): Monat, neueste zuerst, Filter und Summen
    [Fact]
    public async Task The_month_list_shows_one_month_newest_first_with_its_sums()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m, "Giro");
        var cash = await AccountAsync(person, AccountTypes.Cash, 0.00m, "Bargeld");
        await CreateAsync(person, Expense(giro, 10.00m, person.Groceries, new DateOnly(2026, 10, 3)));
        await CreateAsync(person, Expense(cash, 5.00m, person.Groceries, new DateOnly(2026, 10, 8)));
        await CreateAsync(person, Income(giro, 2000.00m, person.Salary));
        await CreateAsync(person, Transfer(giro, cash, 100.00m));

        var all = await MonthAsync(person, 2026, 10);
        var onlyCash = await MonthAsync(person, 2026, 10, cash);
        var september = await MonthAsync(person, 2026, 9);

        Assert.Equal(4, all.Bookings.Count);
        Assert.Equal(all.Bookings.OrderByDescending(b => b.Date).Select(b => b.Date), all.Bookings.Select(b => b.Date));
        Assert.Equal(2000.00m, all.Income);
        Assert.Equal(15.00m, all.Expenses);
        Assert.Equal(2, onlyCash.Bookings.Count);
        Assert.Equal(100.00m, onlyCash.Income);
        Assert.Equal(5.00m, onlyCash.Expenses);
        Assert.Empty(september.Bookings);
    }

    [Theory]
    [InlineData("?year=2026&month=13")]
    [InlineData("?year=2026&month=0")]
    [InlineData("?year=1999&month=5")]
    public async Task A_month_outside_the_calendar_answers_400(string query)
    {
        var person = await SignUpAsync();

        var response = await SendAsync(HttpMethod.Get, Path + query, person);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.MonthInvalid, await response.ReadCodeAsync());
    }

    // A17
    [Fact]
    public async Task Another_persons_booking_answers_404_and_stays_unchanged()
    {
        var anna = await SignUpAsync();
        var ben = await SignUpAsync();
        var annasGiro = await AccountAsync(anna, AccountTypes.Checking, 1000.00m);
        var bensGiro = await AccountAsync(ben, AccountTypes.Checking, 1000.00m);
        var annas = await CreateAsync(anna, Expense(annasGiro, 42.10m, anna.Groceries));

        var put = await SendAsync(HttpMethod.Put, $"{Path}/{annas.Id}", ben, Expense(bensGiro, 1.00m, ben.Groceries));
        var delete = await SendAsync(HttpMethod.Delete, $"{Path}/{annas.Id}", ben);
        var filterOnAnnasAccount = await SendAsync(HttpMethod.Get, $"{Path}?year=2026&month=10&accountId={annasGiro}", ben);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, filterOnAnnasAccount.StatusCode);
        Assert.Empty((await MonthAsync(ben, 2026, 10)).Bookings);
        var unchanged = Assert.Single((await MonthAsync(anna, 2026, 10)).Bookings);
        Assert.Equal(42.10m, unchanged.Amount);
        Assert.Equal(957.90m, await BalanceAsync(anna, annasGiro));
    }

    [Fact]
    public async Task A_booking_on_another_persons_account_answers_404()
    {
        var anna = await SignUpAsync();
        var ben = await SignUpAsync();
        var annasGiro = await AccountAsync(anna, AccountTypes.Checking, 1000.00m);

        var response = await SendAsync(HttpMethod.Post, Path, ben, Expense(annasGiro, 1.00m, ben.Groceries));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1000.00m, await BalanceAsync(anna, annasGiro));
    }

    // Spec 59, A18 (mit echter Datenbank)
    [Fact]
    public async Task A_closed_account_takes_no_new_bookings()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(Active: false));

        var response = await SendAsync(HttpMethod.Post, Path, person, Expense(giro, 1.00m, person.Groceries));

        Assert.Equal(ErrorCodes.AccountClosed, await response.ReadCodeAsync());
    }

    // Spec 58, A10
    [Fact]
    public async Task The_opening_date_may_only_move_to_a_day_before_every_booking()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m, "Giro");
        var cash = await AccountAsync(person, AccountTypes.Cash, 0.00m, "Bargeld");
        await CreateAsync(person, Expense(giro, 1.00m, person.Groceries, October5));
        // Auch eine Umbuchung auf das Konto zählt als Buchung des Kontos.
        await CreateAsync(person, Transfer(cash, giro, 1.00m) with { Date = new DateOnly(2026, 10, 3) });

        var before = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(OpeningDate: new DateOnly(2026, 10, 2)));
        var onIt = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(OpeningDate: new DateOnly(2026, 10, 3)));
        var after = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(OpeningDate: October5));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, onIt.StatusCode);
        Assert.Equal(ErrorCodes.OpeningDateAfterBooking, await onIt.ReadCodeAsync());
        Assert.Equal(ErrorCodes.OpeningDateAfterBooking, await after.ReadCodeAsync());
    }

    // Spec 58, A10 mit den Daten des Kriteriums
    [Fact]
    public async Task With_a_booking_on_the_fifth_the_fourth_is_allowed_and_the_fifth_is_not()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        await CreateAsync(person, Expense(giro, 1.00m, person.Groceries, October5));

        var fourth = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(OpeningDate: new DateOnly(2026, 10, 4)));
        var fifth = await SendAsync(HttpMethod.Patch, $"/api/v1/accounts/{giro}", person, new UpdateAccountRequest(OpeningDate: October5));

        Assert.Equal(HttpStatusCode.OK, fourth.StatusCode);
        Assert.Equal(ErrorCodes.OpeningDateAfterBooking, await fifth.ReadCodeAsync());
    }

    // Spec 59, A7
    [Fact]
    public async Task An_own_category_in_use_cannot_be_deleted_and_an_unused_one_can()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m);
        var create = await SendAsync(HttpMethod.Post, "/api/v1/categories", person, new SaveCategoryRequest("Friseur Kinder", "children", PotKeys.Needs));
        var own = (await create.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var booking = await CreateAsync(person, Expense(giro, 25.00m, own.Id));

        var inUse = await SendAsync(HttpMethod.Delete, $"/api/v1/categories/{own.Id}", person);
        await SendAsync(HttpMethod.Delete, $"{Path}/{booking.Id}", person);
        var unused = await SendAsync(HttpMethod.Delete, $"/api/v1/categories/{own.Id}", person);

        Assert.Equal(HttpStatusCode.BadRequest, inUse.StatusCode);
        Assert.Equal(ErrorCodes.CategoryInUse, await inUse.ReadCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, unused.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_person_deletes_accounts_and_bookings_too()
    {
        var person = await SignUpAsync();
        var giro = await AccountAsync(person, AccountTypes.Checking, 1000.00m, "Giro");
        var cash = await AccountAsync(person, AccountTypes.Cash, 0.00m, "Bargeld");
        var create = await SendAsync(HttpMethod.Post, "/api/v1/categories", person, new SaveCategoryRequest("Eigen", "leisure", PotKeys.Wants));
        var own = (await create.Content.ReadFromJsonAsync<CategoryResponse>())!;
        await CreateAsync(person, Expense(giro, 25.00m, own.Id, pot: PotKeys.Wants));
        await CreateAsync(person, Transfer(giro, cash, 10.00m));

        var delete = await SendAsync(HttpMethod.Delete, "/api/v1/users/me", person, new DeleteMeRequest(Password));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
