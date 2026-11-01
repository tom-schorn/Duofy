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
/// Beide Buchungen des Übertrags stehen am letzten Tag des alten Monats (Toms Entscheidung vom 1.11.2026, Spec 66, R9):
/// Der Kontostand stimmt so an jedem Tag mit dem eingetragenen Stand überein. "Übertrag aus ..." zählt nur über den
/// Planmonat im Folgemonat. Paare, die noch mit dem Ersten gespeichert sind, bleiben unverändert und funktionieren weiter.
/// Die Uhr startet am 20. Oktober 2026 und wird in den Tests vorgestellt; danach meldet sich die Person neu an.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CarryOverLastDayTests : IDisposable
{
    private const string Plans = "/api/v1/plans";
    private const string Accounts = "/api/v1/accounts";
    private const string Bookings = "/api/v1/bookings";
    private const string CarriedOut = "Übertrag nach November";
    private const string CarriedIn = "Übertrag aus Oktober";

    private static readonly DateOnly October20 = new(2026, 10, 20);
    private static readonly DateOnly October31 = new(2026, 10, 31);
    private static readonly DateOnly November1 = new(2026, 11, 1);

    private readonly ApiFixture fixture;
    private readonly MovableTime clock = new(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> api;
    private readonly HttpClient client;

    public CarryOverLastDayTests(ApiFixture fixture)
    {
        this.fixture = fixture;
        api = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(clock)));
        client = api.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        api.Dispose();
    }

    /// <summary>Eine Person, deren Standardkonto im Oktober genau den Rest <paramref name="income"/> minus <paramref name="expenses"/> hat.</summary>
    private async Task<(Person Person, Guid Giro)> PersonWithRestAsync(decimal income, decimal expenses)
    {
        var person = await client.PersonAsync("Anna");
        return (person, await RestOfAsync(person, income, expenses));
    }

    /// <summary>Bucht den Rest auf das erste Konto der Person und gibt dessen Kennung zurück.</summary>
    private async Task<Guid> RestOfAsync(Person person, decimal income, decimal expenses)
    {
        var listed = await client.SendAsync(person, HttpMethod.Get, Accounts);
        var giro = Assert.Single((await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!).Id;
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Income, giro, null, October20, income, null,
            [new SaveBookingLine(income, PotKeys.Income, CategoryCatalog.IdFor("income.earned"))]));
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, giro, null, October20, expenses, null,
            [new SaveBookingLine(expenses, PotKeys.Needs, CategoryCatalog.IdFor("household.groceries"))]));
        return giro;
    }

    private async Task BookAsync(Person person, SaveBookingRequest request)
    {
        var answer = await client.SendAsync(person, HttpMethod.Post, Bookings, request);
        Assert.True(answer.StatusCode == HttpStatusCode.Created, await answer.Content.ReadAsStringAsync());
    }

    private async Task<Guid> OtherAccountAsync(Person person, string name, decimal openingBalance)
    {
        var created = await client.SendAsync(person, HttpMethod.Post, Accounts,
            new CreateAccountRequest(name, AccountTypes.Checking, openingBalance, new DateOnly(2026, 9, 1)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<AccountResponse>())!.Id;
    }

    /// <summary>Der Stand von heute, wie ihn die Kontenseite zeigt.</summary>
    private async Task<decimal> BalanceTodayAsync(Person person, Guid accountId)
    {
        var listed = await client.SendAsync(person, HttpMethod.Get, Accounts);
        listed.EnsureSuccessStatusCode();
        return (await listed.Content.ReadFromJsonAsync<List<AccountResponse>>())!.Single(a => a.Id == accountId).Balance;
    }

    private async Task<PlanResponse> PlanAsync(Person person, int year, int month)
    {
        var answer = await client.SendAsync(person, HttpMethod.Get, $"{Plans}/{year}/{month}");
        answer.EnsureSuccessStatusCode();
        return (await answer.Content.ReadFromJsonAsync<PlanResponse>())!;
    }

    private async Task CarryOverAsync(Person person, decimal amount, params RealBalance[] balances)
    {
        var answer = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/11/carry-over", new CreateCarryOverRequest(amount, balances));
        Assert.True(answer.StatusCode == HttpStatusCode.Created, await answer.Content.ReadAsStringAsync());
    }

    /// <summary>Stellt die Uhr auf den Tag und meldet die Person neu an, denn ihr Token ist danach abgelaufen.</summary>
    private async Task<Person> GoToAsync(Person person, DateOnly day)
    {
        clock.Advance(new DateTimeOffset(day, new TimeOnly(10, 0), TimeSpan.Zero) - clock.GetUtcNow());
        var login = await client.LoginAsync(person.Email);
        login.EnsureSuccessStatusCode();
        return person with { Token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken };
    }

    /// <summary>Eine Buchung des Übertrags, direkt aus der Datenbank: Das Paar steht in keiner Liste der Buchungen.</summary>
    private Task<Booking> PairBookingAsync(Person person, string note) =>
        fixture.Factory.WithDbAsync(db => db.Bookings.AsNoTracking().SingleAsync(b => b.OwnerId == person.Id && b.Note == note));

    /// <summary>Schiebt "Übertrag aus ..." auf den Ersten des Monats, so wie Paare vor dieser Regel gespeichert wurden.</summary>
    private Task StoreAsBeforeTheRuleAsync(Guid incoming) =>
        fixture.Factory.WithDbAsync(db => db.Bookings.Where(b => b.Id == incoming)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.PaymentDate, November1)));

    [Theory]
    [InlineData(3000.00, 2770.00, 230.00)]
    [InlineData(2000.00, 2150.00, -150.00)]
    public async Task On_the_last_day_of_the_month_the_pair_leaves_the_balance_of_today_unchanged(double income, double expenses, double rest)
    {
        var (person, giro) = await PersonWithRestAsync((decimal)income, (decimal)expenses);
        person = await GoToAsync(person, October31);
        Assert.Equal((decimal)rest, await BalanceTodayAsync(person, giro));

        await CarryOverAsync(person, (decimal)rest);

        Assert.Equal((decimal)rest, await BalanceTodayAsync(person, giro));
        Assert.Equal((decimal)rest, (await PlanAsync(person, 2026, 11)).CarryOver.Balances.Single(b => b.AccountId == giro).Balance);
        person = await GoToAsync(person, November1);
        Assert.Equal((decimal)rest, await BalanceTodayAsync(person, giro));
    }

    [Theory]
    [InlineData(3000.00, 2770.00, 230.00)]
    [InlineData(2000.00, 2150.00, -150.00)]
    public async Task Both_bookings_lie_on_the_last_day_and_count_in_their_plan_months(double income, double expenses, double rest)
    {
        var (person, _) = await PersonWithRestAsync((decimal)income, (decimal)expenses);
        person = await GoToAsync(person, October31);

        await CarryOverAsync(person, (decimal)rest);

        var outgoing = await PairBookingAsync(person, CarriedOut);
        var incoming = await PairBookingAsync(person, CarriedIn);
        Assert.Equal((October31, 2026, 10), (outgoing.PaymentDate, outgoing.PlanYear, outgoing.PlanMonth));
        Assert.Equal((October31, 2026, 11), (incoming.PaymentDate, incoming.PlanYear, incoming.PlanMonth));
        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal((decimal)rest, november.CarryOver.Amount);
        // Beide bleiben aus Topf-Gebucht und Quote draußen, in beiden Monaten.
        Assert.All(november.Overview!.Pots, pot => Assert.Equal(0m, pot.Booked));
        Assert.All(november.Quotas, quota => Assert.Equal(0m, quota.Actual));
        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal((decimal)expenses, october.Overview!.Pots.Single(p => p.Pot == PotKeys.Needs).Booked);
    }

    [Theory]
    [InlineData(230.00)]
    [InlineData(-150.00)]
    public async Task The_balance_matches_what_was_entered_on_every_day_on_the_default_account_and_on_one_with_balancing(double rest)
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        var cash = await OtherAccountAsync(person, "Bar", 100.00m);
        person = await GoToAsync(person, October31);

        await CarryOverAsync(person, (decimal)rest, new RealBalance(giro, 200.00m), new RealBalance(cash, 125.00m));

        foreach (var day in new[] { October31, November1, new DateOnly(2026, 11, 30) })
        {
            person = await GoToAsync(person, day);
            Assert.Equal(200.00m, await BalanceTodayAsync(person, giro));
            Assert.Equal(125.00m, await BalanceTodayAsync(person, cash));
        }
    }

    // Die Regel "nichts in der Zukunft" gilt nur für Buchungen von Hand; der Übertrag vor dem Monatsende bleibt erlaubt.
    [Fact]
    public async Task A_carry_over_before_the_end_of_the_month_stays_allowed_and_changes_no_balance_until_and_on_the_last_day()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);

        await CarryOverAsync(person, 230.00m);

        Assert.Equal(October31, (await PairBookingAsync(person, CarriedIn)).PaymentDate);
        Assert.Equal(230.00m, await BalanceTodayAsync(person, giro));
        person = await GoToAsync(person, October31);
        Assert.Equal(230.00m, await BalanceTodayAsync(person, giro));
    }

    [Fact]
    public async Task Removing_and_repeating_keeps_the_balance_and_books_the_pair_again_on_the_last_day()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        person = await GoToAsync(person, October31);
        await CarryOverAsync(person, 230.00m, new RealBalance(giro, 220.00m));

        var removed = await client.SendAsync(person, HttpMethod.Delete, $"{Plans}/2026/11/carry-over");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False((await PlanAsync(person, 2026, 11)).CarryOver.Exists);
        // Der Ausgleich bleibt, nur das Paar geht.
        Assert.Equal(220.00m, await BalanceTodayAsync(person, giro));

        await CarryOverAsync(person, 220.00m, new RealBalance(giro, 220.00m));

        Assert.Equal(220.00m, await BalanceTodayAsync(person, giro));
        Assert.Equal(October31, (await PairBookingAsync(person, CarriedIn)).PaymentDate);
        Assert.Equal(1, await fixture.Factory.WithDbAsync(db => db.Bookings.CountAsync(b => b.OwnerId == person.Id && b.Note == "Ausgleich")));
    }

    // Die Einnahme nutzt die Spanne von R7: ein Monat nach dem Datum, auch über den Jahreswechsel und im Februar.
    [Theory]
    [InlineData(2026, 11, "Oktober")]
    [InlineData(2027, 1, "Dezember")]
    [InlineData(2026, 3, "Februar")]
    public async Task The_plan_month_of_the_income_is_one_a_booking_on_that_date_may_have(int year, int month, string previous)
    {
        var (person, _) = await PersonWithRestAsync(3000.00m, 2770.00m);
        await fixture.Factory.WithDbAsync(db => db.Accounts.Where(a => a.OwnerId == person.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.OpeningDate, new DateOnly(2026, 1, 1))));

        var answer = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/{year}/{month}/carry-over", new CreateCarryOverRequest(230.00m));

        Assert.True(answer.StatusCode == HttpStatusCode.Created, await answer.Content.ReadAsStringAsync());
        var incoming = await PairBookingAsync(person, $"Übertrag aus {previous}");
        Assert.Equal(new DateOnly(year, month, 1).AddDays(-1), incoming.PaymentDate);
        Assert.True(Booking.IsPlanMonthAllowed(incoming.PaymentDate, incoming.PlanYear, incoming.PlanMonth));
    }

    [Fact]
    public async Task A_pair_stored_with_the_first_of_the_month_keeps_working()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        person = await GoToAsync(person, October31);
        await CarryOverAsync(person, 230.00m);
        var incoming = await PairBookingAsync(person, CarriedIn);
        await StoreAsBeforeTheRuleAsync(incoming.Id);

        // Gelesen wird es wie gespeichert: Die Regel ändert nichts an der Vergangenheit, auch nicht das Minus am Monatsletzten.
        Assert.Equal(0m, await BalanceTodayAsync(person, giro));
        var november = await PlanAsync(person, 2026, 11);
        Assert.True(november.CarryOver.Exists);
        Assert.Equal(230.00m, november.CarryOver.Amount);
        Assert.All(november.Quotas, q => Assert.Equal(0m, q.Actual));
        Assert.All(november.Overview!.Pots, pot => Assert.Equal(0m, pot.Booked));

        // Geändert wird es wie jedes Paar nur über den Plan.
        var line = new SaveBookingLine(1.00m, PotKeys.Income, CategoryCatalog.IdFor("income.other"));
        var put = await client.SendAsync(person, HttpMethod.Put, $"{Bookings}/{incoming.Id}",
            new SaveBookingRequest(BookingKinds.Income, giro, null, November1, 1.00m, null, [line], 2026, 11));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.CarryOverBooking), (put.StatusCode, await put.ReadCodeAsync()));

        person = await GoToAsync(person, November1);
        Assert.Equal(230.00m, await BalanceTodayAsync(person, giro));

        // Zurücknehmen löscht beide, erneut ausführen bucht nach der neuen Regel.
        var removed = await client.SendAsync(person, HttpMethod.Delete, $"{Plans}/2026/11/carry-over");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(230.00m, await BalanceTodayAsync(person, giro));

        await CarryOverAsync(person, 230.00m);

        Assert.Equal(October31, (await PairBookingAsync(person, CarriedIn)).PaymentDate);
        Assert.Equal(230.00m, await BalanceTodayAsync(person, giro));
    }

    // Der Haushalt zeigt den Stand am Monatsletzten: Der Übertrag darf ihn nicht senken.
    [Fact]
    public async Task The_household_shows_the_entered_balance_on_the_last_day_of_the_month()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        var household = await client.FoundAsync(person);
        person = await GoToAsync(person, October31);
        await CarryOverAsync(person, 230.00m);

        var answer = await client.SendAsync(person, HttpMethod.Get, $"/api/v1/households/{household.Id}/plans/2026/10");

        answer.EnsureSuccessStatusCode();
        var plan = (await answer.Content.ReadFromJsonAsync<HouseholdPlanResponse>())!;
        Assert.Equal(230.00m, plan.Accounts.Single(a => a.AccountId == giro).Balance);
    }

    private async Task<BookingMonthResponse> BookingPageAsync(Person person, int year, int month, string? query = null)
    {
        var answer = await client.SendAsync(person, HttpMethod.Get, $"{Bookings}?year={year}&month={month}{query}");
        answer.EnsureSuccessStatusCode();
        return (await answer.Content.ReadFromJsonAsync<BookingMonthResponse>())!;
    }

    private static void HasNoHalf(IEnumerable<BookingResponse> bookings) =>
        Assert.DoesNotContain(bookings, b => b.Note is CarriedOut or CarriedIn);

    // Die Seite Buchungen zeigt nur echtes Geld: das Paar weder als Zeile noch in einer Summe, auch mit Kontofilter.
    [Theory]
    [InlineData(3000.00, 2770.00, 230.00)]
    [InlineData(2000.00, 2150.00, -150.00)]
    public async Task The_booking_page_shows_neither_half_and_counts_neither_in_its_sums(double income, double expenses, double rest)
    {
        var (person, giro) = await PersonWithRestAsync((decimal)income, (decimal)expenses);
        person = await GoToAsync(person, October31);

        await CarryOverAsync(person, (decimal)rest);

        foreach (var query in new[] { null, $"&accountId={giro}" })
        {
            var october = await BookingPageAsync(person, 2026, 10, query);
            Assert.Equal(2, october.Bookings.Count);
            HasNoHalf(october.Bookings);
            Assert.Equal(((decimal)income, (decimal)expenses), (october.Income, october.Expenses));
            var november = await BookingPageAsync(person, 2026, 11, query);
            Assert.Empty(november.Bookings);
            Assert.Equal((0m, 0m), (november.Income, november.Expenses));
        }
    }

    [Theory]
    [InlineData(3000.00, 2770.00, 230.00)]
    [InlineData(2000.00, 2150.00, -150.00)]
    public async Task The_month_shows_each_half_as_its_own_line_and_not_as_a_booking(double income, double expenses, double rest)
    {
        var (person, _) = await PersonWithRestAsync((decimal)income, (decimal)expenses);
        var freeBefore = (await PlanAsync(person, 2026, 10)).Overview!.Free;
        person = await GoToAsync(person, October31);

        await CarryOverAsync(person, (decimal)rest);

        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal(2, october.Overview!.Bookings.Count);
        HasNoHalf(october.Overview.Bookings);
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, (decimal)rest), Assert.Single(october.Overview.CarryOvers!));
        Assert.Equal(freeBefore, october.Overview.Free);
        var november = await PlanAsync(person, 2026, 11);
        Assert.Empty(november.Overview!.Bookings);
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, (decimal)rest), Assert.Single(november.Overview.CarryOvers!));
    }

    // Toms Beispiel: Die Summen im Plan zeigen nur echtes Geld, der Übertrag steht in seiner eigenen Zeile.
    [Theory]
    [InlineData(3000.00, 2770.00, 230.00)]
    [InlineData(2000.00, 2150.00, -150.00)]
    public async Task The_booked_sums_of_the_plan_count_only_real_money_and_only_the_new_month_gets_free_money(double income, double expenses, double rest)
    {
        var (person, _) = await PersonWithRestAsync((decimal)income, (decimal)expenses);
        var freeOfOctober = (await PlanAsync(person, 2026, 10)).Overview!.Free;
        var freeOfNovember = (await PlanAsync(person, 2026, 11)).Overview!.Free;

        await CarryOverAsync(person, (decimal)rest);

        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal(((decimal)income, (decimal)expenses), (october.BookedIncome, october.BookedExpenses));
        Assert.Equal(freeOfOctober, october.Overview!.Free);
        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal((0m, 0m), (november.BookedIncome, november.BookedExpenses));
        Assert.Equal(freeOfNovember + (decimal)rest, november.Overview!.Free);
    }

    // Der Vorschlag rechnet getrennt von den Summen: Der Rest des Vormonats gehört in den nächsten Rest.
    [Fact]
    public async Task The_suggestion_keeps_the_carried_rest_over_two_months_in_a_row()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        Assert.Equal(230.00m, (await PlanAsync(person, 2026, 11)).CarryOver.Suggested);
        await CarryOverAsync(person, 230.00m);

        // Im November 1000 verdient und 800 ausgegeben, gebucht schon im Oktober mit dem Planmonat November.
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Income, giro, null, October20, 1000.00m, null,
            [new SaveBookingLine(1000.00m, PotKeys.Income, CategoryCatalog.IdFor("income.earned"))], 2026, 11));
        await BookAsync(person, new SaveBookingRequest(BookingKinds.Expense, giro, null, October20, 800.00m, null,
            [new SaveBookingLine(800.00m, PotKeys.Needs, CategoryCatalog.IdFor("household.groceries"))], 2026, 11));

        var november = await PlanAsync(person, 2026, 11);
        Assert.Equal((1000.00m, 800.00m), (november.BookedIncome, november.BookedExpenses));
        Assert.Equal(430.00m, (await PlanAsync(person, 2026, 12)).CarryOver.Suggested);

        var second = await client.SendAsync(person, HttpMethod.Post, $"{Plans}/2026/12/carry-over", new CreateCarryOverRequest(430.00m));
        Assert.True(second.StatusCode == HttpStatusCode.Created, await second.Content.ReadAsStringAsync());

        Assert.Equal(430.00m, (await PlanAsync(person, 2027, 1)).CarryOver.Suggested);
        Assert.Equal((1000.00m, 800.00m), ((await PlanAsync(person, 2026, 11)).BookedIncome, (await PlanAsync(person, 2026, 11)).BookedExpenses));
        Assert.Equal(
            [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 230.00m), new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 12, 430.00m)],
            (await PlanAsync(person, 2026, 11)).Overview!.CarryOvers!);
    }

    // Der Ausgleich ist echtes Geld und bleibt eine normale Buchung.
    [Fact]
    public async Task A_balancing_booking_stays_a_normal_booking_on_the_page_and_in_the_month()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        person = await GoToAsync(person, October31);

        await CarryOverAsync(person, 230.00m, new RealBalance(giro, 220.00m));

        var page = await BookingPageAsync(person, 2026, 10);
        Assert.Single(page.Bookings, b => b.Note == "Ausgleich");
        Assert.Equal((3000.00m, 2780.00m), (page.Income, page.Expenses));
        Assert.Single((await PlanAsync(person, 2026, 10)).Overview!.Bookings, b => b.Note == "Ausgleich");
    }

    // Alte Paare (Einnahme am Ersten) gelten über die im Plan gespeicherten Kennungen genauso als Übertrag.
    [Fact]
    public async Task A_pair_stored_with_the_first_of_the_month_is_hidden_and_shown_as_lines_the_same_way()
    {
        var (person, giro) = await PersonWithRestAsync(3000.00m, 2770.00m);
        person = await GoToAsync(person, October31);
        await CarryOverAsync(person, 230.00m);
        var incoming = await PairBookingAsync(person, CarriedIn);
        var outgoing = await PairBookingAsync(person, CarriedOut);
        await StoreAsBeforeTheRuleAsync(incoming.Id);

        Assert.Empty((await BookingPageAsync(person, 2026, 11)).Bookings);
        HasNoHalf((await BookingPageAsync(person, 2026, 10)).Bookings);
        var november = await PlanAsync(person, 2026, 11);
        Assert.Empty(november.Overview!.Bookings);
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 230.00m), Assert.Single(november.Overview.CarryOvers!));
        var october = await PlanAsync(person, 2026, 10);
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, 230.00m), Assert.Single(october.Overview!.CarryOvers!));
        foreach (var id in new[] { incoming.Id, outgoing.Id })
        {
            var delete = await client.SendAsync(person, HttpMethod.Delete, $"{Bookings}/{id}");
            Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.CarryOverBooking), (delete.StatusCode, await delete.ReadCodeAsync()));
        }
        // Am Monatsletzten bleibt die Anzeige alter Paare, wie sie gespeichert sind; ab dem Ersten stimmt der Stand.
        Assert.Equal(0m, await BalanceTodayAsync(person, giro));
    }

    // Spec 68, R4: Die Zeile gehört zu den Buchungen; ohne deren Freigabe gibt es sie nicht, die Liste bleibt ohne Paar.
    [Fact]
    public async Task Someone_with_a_grant_sees_the_lines_only_with_the_bookings_and_never_the_halves()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        await RestOfAsync(anna, 3000.00m, 2770.00m);
        await CarryOverAsync(anna, 230.00m);
        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View));

        var hidden = await client.SendAsync(ben, HttpMethod.Get, $"{Plans}/2026/11?owner={anna.Id}");
        hidden.EnsureSuccessStatusCode();
        var withoutBook = (await hidden.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Empty(withoutBook.Overview!.CarryOvers ?? []);
        Assert.Null(withoutBook.CarryOver.Amount);

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Plan: AccessLevelKeys.View, Book: AccessLevelKeys.View));

        var shown = await client.SendAsync(ben, HttpMethod.Get, $"{Plans}/2026/11?owner={anna.Id}");
        shown.EnsureSuccessStatusCode();
        var withBook = (await shown.Content.ReadFromJsonAsync<PlanResponse>())!;
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 10, 230.00m), Assert.Single(withBook.Overview!.CarryOvers!));
        Assert.Empty(withBook.Overview.Bookings);
        var page = await BookingPageAsync(ben, 2026, 10, $"&owner={anna.Id}");
        Assert.Equal(2, page.Bookings.Count);
        HasNoHalf(page.Bookings);
    }

    // Spec 46, R3 und R4: keine neue Offenlegung, die Zahlen bleiben gleich, keine Kennung einer verborgenen Buchung.
    [Fact]
    public async Task The_household_shows_the_lines_only_with_the_grant_and_its_figures_stay_the_same()
    {
        var (anna, ben) = await client.AnnaAndBenAsync();
        var household = await client.MineAsync(anna);
        await RestOfAsync(anna, 3000.00m, 2770.00m);
        await CarryOverAsync(anna, 230.00m);
        var incoming = await PairBookingAsync(anna, CarriedIn);
        var outgoing = await PairBookingAsync(anna, CarriedOut);
        var path = $"/api/v1/households/{household.Id}/plans/2026/10";

        var withoutGrant = await client.SendAsync(ben, HttpMethod.Get, path);
        withoutGrant.EnsureSuccessStatusCode();
        var text = await withoutGrant.Content.ReadAsStringAsync();
        var hidden = (await withoutGrant.Content.ReadFromJsonAsync<HouseholdPlanResponse>())!;
        Assert.Empty(hidden.CarryOvers ?? []);
        Assert.Contains("Anna", hidden.BookingsMissing!);
        Assert.DoesNotContain(incoming.Id.ToString(), text);
        Assert.DoesNotContain(outgoing.Id.ToString(), text);

        await client.GrantAsync(anna, ben, new UpdateGrantsRequest(Book: AccessLevelKeys.View));

        var withGrant = await client.SendAsync(ben, HttpMethod.Get, path);
        withGrant.EnsureSuccessStatusCode();
        var shownText = await withGrant.Content.ReadAsStringAsync();
        var shown = (await withGrant.Content.ReadFromJsonAsync<HouseholdPlanResponse>())!;
        Assert.Equal(new PlanCarryOverLineResponse(CarryOverDirections.Out, 2026, 11, 230.00m, "Anna"), Assert.Single(shown.CarryOvers!));
        Assert.Equal(2, shown.Bookings!.Count);
        Assert.DoesNotContain(outgoing.Id.ToString(), shownText);
        Assert.Equal(
            (hidden.Income, hidden.Fixed, hidden.Free, hidden.Unpaid, hidden.Unplanned),
            (shown.Income, shown.Fixed, shown.Free, shown.Unpaid, shown.Unplanned));
        Assert.Equal(hidden.Pots, shown.Pots);
        Assert.Equal(hidden.Quotas, shown.Quotas);
    }
}
