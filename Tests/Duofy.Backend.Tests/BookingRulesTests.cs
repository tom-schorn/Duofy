using Duofy.Schemas;

namespace Duofy.Api.Tests;

/// <summary>
/// Die Regeln für Buchungen (R1 bis R6, R11, R13), geprüft ohne Datenbank gegen einen
/// Ersatz für die Konten und die Kategorien. Die Tabelle der Konten gibt es erst mit
/// der Kontoverwaltung; bis dahin ist <see cref="IAccountLookup"/> die Naht.
/// </summary>
public class BookingRulesTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    // "Heute" ist der 21. Oktober 2026 in Berlin.
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeAccounts accounts = new();
    private readonly FakeCategories categories = new();
    private readonly FixedTime time = new(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

    private readonly Guid giro;
    private readonly Guid cash;
    private readonly Guid groceries;
    private readonly Guid personalCare;
    private readonly Guid salary;

    public BookingRulesTests()
    {
        giro = accounts.Add(Owner, openedOn: new DateOnly(2026, 10, 1));
        cash = accounts.Add(Owner, openedOn: new DateOnly(2026, 10, 1));
        groceries = categories.Add(Owner, isIncome: false);
        personalCare = categories.Add(Owner, isIncome: false);
        salary = categories.Add(Owner, isIncome: true);
    }

    private Task<BookingCheck> CheckAsync(SaveBookingRequest request) =>
        BookingValidator.CheckAsync(request, Owner, accounts, categories, time, CancellationToken.None);

    private SaveBookingRequest Expense(decimal amount = 42.10m, DateOnly? date = null, params SaveBookingLine[] lines) => new(
        BookingKinds.Expense, giro, null, date ?? Today, amount, null,
        lines.Length > 0 ? lines : [new SaveBookingLine(amount, PotKeys.Needs, groceries)]);

    private static void AssertInvalid(BookingCheck check, string code)
    {
        Assert.Equal(BookingCheckOutcome.Invalid, check.Outcome);
        Assert.Equal(code, check.Code);
    }

    [Fact]
    public async Task A_plain_expense_is_valid()
    {
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense())).Outcome);
    }

    [Fact]
    public async Task An_income_with_an_income_category_is_valid()
    {
        var request = new SaveBookingRequest(
            BookingKinds.Income, giro, null, Today, 2450m, null, [new SaveBookingLine(2450m, PotKeys.Income, salary)]);

        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(request)).Outcome);
    }

    // A2 (Teil: Betrag 0 oder negativ)
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Amount_must_be_positive(int amount)
    {
        AssertInvalid(await CheckAsync(Expense(amount)), ErrorCodes.AmountInvalid);
    }

    [Fact]
    public async Task Amount_has_at_most_two_decimals_and_a_maximum()
    {
        AssertInvalid(await CheckAsync(Expense(1.005m)), ErrorCodes.AmountInvalid);
        AssertInvalid(await CheckAsync(Expense(BookingRules.MaxAmount + 0.01m)), ErrorCodes.AmountInvalid);
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense(BookingRules.MaxAmount))).Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Expense")]
    [InlineData("refund")]
    public async Task Kind_must_be_one_of_the_three(string? kind)
    {
        AssertInvalid(await CheckAsync(Expense() with { Kind = kind }), ErrorCodes.KindInvalid);
    }

    [Fact]
    public async Task Note_may_have_200_characters_but_not_201()
    {
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense() with { Note = new string('x', 200) })).Outcome);
        AssertInvalid(await CheckAsync(Expense() with { Note = new string('x', 201) }), ErrorCodes.NoteTooLong);
    }

    // A3
    [Fact]
    public async Task Expense_line_without_a_pot_answers_pot_required()
    {
        var request = Expense(lines: new SaveBookingLine(42.10m, null, groceries));

        AssertInvalid(await CheckAsync(request), ErrorCodes.PotRequired);
    }

    [Fact]
    public async Task Expense_line_without_a_category_answers_category_required()
    {
        var request = Expense(lines: new SaveBookingLine(42.10m, PotKeys.Needs, null));

        AssertInvalid(await CheckAsync(request), ErrorCodes.CategoryRequired);
    }

    [Fact]
    public async Task Expense_without_lines_answers_lines_required()
    {
        var request = Expense() with { Lines = [] };

        AssertInvalid(await CheckAsync(request), ErrorCodes.LinesRequired);
        AssertInvalid(await CheckAsync(request with { Lines = null }), ErrorCodes.LinesRequired);
    }

    [Fact]
    public async Task A_booking_has_at_most_20_lines()
    {
        SaveBookingLine[] Lines(int count) =>
            Enumerable.Range(0, count).Select(_ => new SaveBookingLine(1m, PotKeys.Needs, groceries)).ToArray();

        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense(20m, null, Lines(20)))).Outcome);
        AssertInvalid(await CheckAsync(Expense(21m, null, Lines(21))), ErrorCodes.LinesTooMany);
    }

    // A19 (Teil: Prüfung)
    [Fact]
    public async Task A_split_expense_whose_lines_add_up_is_valid()
    {
        var request = Expense(80m, null,
            new SaveBookingLine(60m, PotKeys.Needs, groceries),
            new SaveBookingLine(20m, PotKeys.Needs, personalCare));

        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(request)).Outcome);
    }

    // A20
    [Fact]
    public async Task Lines_that_do_not_add_up_answer_lines_sum_mismatch()
    {
        var request = Expense(80m, null,
            new SaveBookingLine(50m, PotKeys.Needs, groceries),
            new SaveBookingLine(20m, PotKeys.Needs, personalCare));

        AssertInvalid(await CheckAsync(request), ErrorCodes.LinesSumMismatch);
    }

    // A20
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_line_amount_must_be_positive(int lineAmount)
    {
        var request = Expense(80m, null,
            new SaveBookingLine(80m - lineAmount, PotKeys.Needs, groceries),
            new SaveBookingLine(lineAmount, PotKeys.Needs, personalCare));

        AssertInvalid(await CheckAsync(request), ErrorCodes.LineAmountInvalid);
    }

    // A20
    [Fact]
    public async Task Income_line_in_pot_needs_answers_line_pot_invalid()
    {
        var request = new SaveBookingRequest(
            BookingKinds.Income, giro, null, Today, 100m, null, [new SaveBookingLine(100m, PotKeys.Needs, salary)]);

        AssertInvalid(await CheckAsync(request), ErrorCodes.LinePotInvalid);
    }

    [Fact]
    public async Task Expense_line_in_pot_income_or_an_unknown_pot_answers_line_pot_invalid()
    {
        AssertInvalid(await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, PotKeys.Income, groceries))), ErrorCodes.LinePotInvalid);
        AssertInvalid(await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, "nonsense", groceries))), ErrorCodes.LinePotInvalid);
    }

    // R3: Einnahmen nur mit Einnahme-Kategorien, Ausgaben nur mit den anderen.
    [Fact]
    public async Task Income_needs_an_income_category_and_expenses_may_not_use_one()
    {
        var income = new SaveBookingRequest(
            BookingKinds.Income, giro, null, Today, 100m, null, [new SaveBookingLine(100m, PotKeys.Income, groceries)]);

        AssertInvalid(await CheckAsync(income), ErrorCodes.LineCategoryInvalid);
        AssertInvalid(await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, PotKeys.Needs, salary))), ErrorCodes.LineCategoryInvalid);
    }

    // A8 (Teil: Prüfung)
    [Fact]
    public async Task A_transfer_between_two_accounts_is_valid()
    {
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Transfer())).Outcome);
    }

    // Spec 72, R9: Quelle und Ziel sind frei aus den drei Töpfen wählbar, auch derselbe Topf auf beiden Seiten.
    [Theory]
    [InlineData(PotKeys.Savings, PotKeys.Wants)]
    [InlineData(PotKeys.Needs, PotKeys.Savings)]
    [InlineData(PotKeys.Wants, PotKeys.Wants)]
    public async Task A_transfer_with_any_source_and_target_pot_is_valid(string source, string target)
    {
        var request = Transfer() with { Lines = [new SaveBookingLine(100m, source, null)], TargetPot = target };

        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(request)).Outcome);
    }

    // Spec 72, R9: Fehlt einer der beiden Töpfe, lehnt die API ab.
    [Fact]
    public async Task A_transfer_without_its_source_or_target_pot_answers_transfer_pots_required()
    {
        AssertInvalid(await CheckAsync(Transfer() with { Lines = null }), ErrorCodes.TransferPotsRequired);
        AssertInvalid(await CheckAsync(Transfer() with { Lines = [] }), ErrorCodes.TransferPotsRequired);
        AssertInvalid(await CheckAsync(Transfer() with { Lines = [new SaveBookingLine(100m, null, null)] }), ErrorCodes.TransferPotsRequired);
        AssertInvalid(await CheckAsync(Transfer() with { TargetPot = null }), ErrorCodes.TransferPotsRequired);
    }

    // A9
    [Fact]
    public async Task Transfer_to_the_same_account_answers_transfer_same_account()
    {
        AssertInvalid(await CheckAsync(Transfer() with { CounterAccountId = giro }), ErrorCodes.TransferSameAccount);
    }

    // Spec 72, R9: Der Topf Einnahmen und unbekannte Töpfe sind weder Quelle noch Ziel.
    [Fact]
    public async Task Transfer_with_the_income_pot_or_an_unknown_pot_answers_transfer_pot_invalid()
    {
        AssertInvalid(await CheckAsync(Transfer() with { Lines = [new SaveBookingLine(100m, PotKeys.Income, null)] }), ErrorCodes.TransferPotInvalid);
        AssertInvalid(await CheckAsync(Transfer() with { TargetPot = PotKeys.Income }), ErrorCodes.TransferPotInvalid);
        AssertInvalid(await CheckAsync(Transfer() with { TargetPot = "luxury" }), ErrorCodes.TransferPotInvalid);
    }

    // A9
    [Fact]
    public async Task Transfer_with_a_category_answers_transfer_category_invalid()
    {
        var request = Transfer() with { Lines = [new SaveBookingLine(100m, PotKeys.Savings, groceries)] };

        AssertInvalid(await CheckAsync(request), ErrorCodes.TransferCategoryInvalid);
    }

    [Fact]
    public async Task Transfer_needs_a_counter_account_and_expense_may_not_have_one()
    {
        AssertInvalid(await CheckAsync(Transfer() with { CounterAccountId = null }), ErrorCodes.CounterAccountRequired);
        AssertInvalid(await CheckAsync(Expense() with { CounterAccountId = cash }), ErrorCodes.CounterAccountUnexpected);
    }

    [Fact]
    public async Task Transfer_with_two_lines_or_a_line_of_another_amount_is_invalid()
    {
        var twoLines = Transfer() with
        {
            Lines = [new SaveBookingLine(50m, PotKeys.Savings, null), new SaveBookingLine(50m, PotKeys.Savings, null)],
        };
        var otherAmount = Transfer() with { Lines = [new SaveBookingLine(90m, PotKeys.Savings, null)] };

        AssertInvalid(await CheckAsync(twoLines), ErrorCodes.LinesTooMany);
        AssertInvalid(await CheckAsync(otherAmount), ErrorCodes.LinesSumMismatch);
    }

    // A11
    [Fact]
    public async Task Date_on_the_opening_date_or_before_answers_date_before_opening()
    {
        AssertInvalid(await CheckAsync(Expense(date: new DateOnly(2026, 10, 1))), ErrorCodes.DateBeforeOpening);
        AssertInvalid(await CheckAsync(Expense(date: new DateOnly(2026, 9, 15))), ErrorCodes.DateBeforeOpening);
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense(date: new DateOnly(2026, 10, 2)))).Outcome);
    }

    // A11
    [Fact]
    public async Task Date_after_today_answers_date_in_future()
    {
        AssertInvalid(await CheckAsync(Expense(date: Today.AddDays(1))), ErrorCodes.DateInFuture);
        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense(date: Today))).Outcome);
    }

    [Fact]
    public async Task Today_is_the_day_in_Berlin_not_in_UTC()
    {
        // 22:30 Uhr UTC am 20. Oktober ist in Berlin (Sommerzeit) schon 00:30 Uhr am 21.
        time.Now = new DateTimeOffset(2026, 10, 20, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(BookingCheckOutcome.Valid, (await CheckAsync(Expense(date: new DateOnly(2026, 10, 21)))).Outcome);
        AssertInvalid(await CheckAsync(Expense(date: new DateOnly(2026, 10, 22))), ErrorCodes.DateInFuture);
    }

    [Fact]
    public async Task Both_accounts_of_a_transfer_count_for_the_opening_date()
    {
        var late = accounts.Add(Owner, openedOn: new DateOnly(2026, 10, 15));
        var request = Transfer() with { CounterAccountId = late, Date = new DateOnly(2026, 10, 10) };

        AssertInvalid(await CheckAsync(request), ErrorCodes.DateBeforeOpening);
    }

    // A18
    [Fact]
    public async Task Booking_on_a_closed_account_answers_account_closed()
    {
        var closed = accounts.Add(Owner, openedOn: new DateOnly(2026, 10, 1), active: false);

        AssertInvalid(await CheckAsync(Expense() with { AccountId = closed }), ErrorCodes.AccountClosed);
        AssertInvalid(await CheckAsync(Transfer() with { CounterAccountId = closed }), ErrorCodes.AccountClosed);
    }

    // A17 / R11: fremde und unbekannte Kennungen sind "nicht gefunden"
    [Fact]
    public async Task Account_of_another_person_or_unknown_is_not_found()
    {
        var foreign = accounts.Add(Guid.NewGuid(), openedOn: new DateOnly(2026, 10, 1));

        Assert.Equal(BookingCheckOutcome.NotFound, (await CheckAsync(Expense() with { AccountId = foreign })).Outcome);
        Assert.Equal(BookingCheckOutcome.NotFound, (await CheckAsync(Expense() with { AccountId = Guid.NewGuid() })).Outcome);
        Assert.Equal(BookingCheckOutcome.NotFound, (await CheckAsync(Transfer() with { CounterAccountId = foreign })).Outcome);
    }

    [Fact]
    public async Task Category_of_another_person_or_unknown_is_not_found()
    {
        var foreign = categories.Add(Guid.NewGuid(), isIncome: false);

        Assert.Equal(BookingCheckOutcome.NotFound,
            (await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, PotKeys.Needs, foreign)))).Outcome);
        Assert.Equal(BookingCheckOutcome.NotFound,
            (await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, PotKeys.Needs, Guid.NewGuid())))).Outcome);
    }

    [Fact]
    public async Task Built_in_categories_are_visible_to_everybody()
    {
        var builtIn = categories.Add(owner: null, isIncome: false);

        Assert.Equal(BookingCheckOutcome.Valid,
            (await CheckAsync(Expense(lines: new SaveBookingLine(42.10m, PotKeys.Needs, builtIn)))).Outcome);
    }

    private SaveBookingRequest Transfer() => new(
        BookingKinds.Transfer, giro, cash, Today, 100m, null, [new SaveBookingLine(100m, PotKeys.Savings, null)], TargetPot: PotKeys.Wants);

    private sealed class FakeAccounts : IAccountLookup
    {
        private readonly Dictionary<Guid, AccountInfo> byId = [];

        public Guid Add(Guid owner, DateOnly openedOn, bool active = true, bool isDefault = false)
        {
            var id = Guid.NewGuid();
            byId[id] = new AccountInfo(id, owner, active, openedOn, isDefault);
            return id;
        }

        public Task<AccountInfo?> FindAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(byId.TryGetValue(accountId, out var info) && info.OwnerId == ownerId ? info : null);
    }

    private sealed class FakeCategories : ICategoryLookup
    {
        private readonly Dictionary<Guid, (Guid? Owner, CategoryInfo Info)> byId = [];

        public Guid Add(Guid? owner, bool isIncome)
        {
            var id = Guid.NewGuid();
            byId[id] = (owner, new CategoryInfo(id, isIncome));
            return id;
        }

        public Task<CategoryInfo?> FindAsync(Guid ownerId, Guid categoryId, CancellationToken cancellationToken) =>
            Task.FromResult(byId.TryGetValue(categoryId, out var entry) && (entry.Owner is null || entry.Owner == ownerId)
                ? entry.Info
                : null);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
