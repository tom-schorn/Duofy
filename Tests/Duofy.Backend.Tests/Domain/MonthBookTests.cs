using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Das Monatsbuch: welche Buchungen die Seite zeigt und was die Summen oben zählen (R8).</summary>
public class MonthBookTests
{
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();

    [Fact]
    public void Shows_only_the_bookings_of_the_month_newest_first()
    {
        var older = Expense(Giro, new DateOnly(2026, 10, 3), 10m);
        var newer = Expense(Giro, new DateOnly(2026, 10, 20), 20m);
        var september = Expense(Giro, new DateOnly(2026, 9, 30), 30m);
        var november = Expense(Giro, new DateOnly(2026, 11, 1), 40m);

        var shown = MonthBook.Select([older, september, newer, november], 2026, 10, accountId: null);

        Assert.Equal([newer, older], shown);
    }

    [Fact]
    public void The_first_and_last_day_belong_to_the_month()
    {
        var first = Expense(Giro, new DateOnly(2026, 10, 1), 1m);
        var last = Expense(Giro, new DateOnly(2026, 10, 31), 1m);

        var shown = MonthBook.Select([first, last], 2026, 10, accountId: null);

        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public void Filter_keeps_bookings_that_touch_the_account_as_from_or_to_account()
    {
        var onGiro = Expense(Giro, new DateOnly(2026, 10, 5), 10m);
        var onCash = Expense(Cash, new DateOnly(2026, 10, 6), 5m);
        var transferOut = Transfer(Giro, Cash, new DateOnly(2026, 10, 7), 100m);
        var transferBetweenOthers = Transfer(Savings, Cash, new DateOnly(2026, 10, 8), 100m);

        var shown = MonthBook.Select([onGiro, onCash, transferOut, transferBetweenOthers], 2026, 10, Giro);

        Assert.Equal([transferOut, onGiro], shown);
    }

    [Fact]
    public void Sums_count_income_and_expenses_and_leave_transfers_out_without_a_filter()
    {
        var salary = Income(Giro, new DateOnly(2026, 10, 1), 2450m);
        var groceries = Expense(Giro, new DateOnly(2026, 10, 2), 42.10m);
        var rent = Expense(Giro, new DateOnly(2026, 10, 3), 800m);
        var transfer = Transfer(Giro, Cash, new DateOnly(2026, 10, 4), 100m);

        var sums = MonthBook.SumsOf([salary, groceries, rent, transfer], accountId: null);

        Assert.Equal(2450m, sums.Income);
        Assert.Equal(842.10m, sums.Expenses);
    }

    [Fact]
    public void With_a_filter_transfers_count_as_income_or_expense_of_that_account()
    {
        var salary = Income(Giro, new DateOnly(2026, 10, 1), 2450m);
        var transferOut = Transfer(Giro, Cash, new DateOnly(2026, 10, 4), 100m);
        var transferIn = Transfer(Savings, Giro, new DateOnly(2026, 10, 5), 30m);

        var sums = MonthBook.SumsOf([salary, transferOut, transferIn], Giro);

        Assert.Equal(2480m, sums.Income);
        Assert.Equal(100m, sums.Expenses);
    }

    [Fact]
    public void An_empty_month_has_zero_sums()
    {
        var sums = MonthBook.SumsOf([], accountId: null);

        Assert.Equal(0m, sums.Income);
        Assert.Equal(0m, sums.Expenses);
    }

    [Fact]
    public void Sums_go_by_the_pots_of_the_lines()
    {
        // Ein Kassenzettel über 100,00 Euro: 60,00 Grundbedarf, 25,00 Wünsche, 15,00 Sparen.
        var split = new Booking { AccountId = Giro, PaymentDate = new DateOnly(2026, 10, 2), Amount = 100m };
        split.Lines.Add(new BookingLine { Amount = 60m, Pot = Pot.Needs });
        split.Lines.Add(new BookingLine { Amount = 25m, Pot = Pot.Wants });
        split.Lines.Add(new BookingLine { Amount = 15m, Pot = Pot.Savings });

        var sums = MonthBook.SumsOf([split], accountId: null);

        Assert.Equal(0m, sums.Income);
        Assert.Equal(100m, sums.Expenses);
    }

    [Fact]
    public void Split_income_counts_the_sum_of_its_income_lines()
    {
        var split = new Booking { AccountId = Giro, PaymentDate = new DateOnly(2026, 10, 1), Amount = 2500m };
        split.Lines.Add(new BookingLine { Amount = 2400m, Pot = Pot.Income });
        split.Lines.Add(new BookingLine { Amount = 100m, Pot = Pot.Income });

        Assert.Equal(2500m, MonthBook.SumsOf([split], accountId: null).Income);
    }

    private static Booking Expense(Guid account, DateOnly on, decimal amount) => WithLine(account, on, amount, Pot.Needs);

    private static Booking Income(Guid account, DateOnly on, decimal amount) => WithLine(account, on, amount, Pot.Income);

    private static Booking WithLine(Guid account, DateOnly on, decimal amount, Pot pot)
    {
        var booking = new Booking { AccountId = account, PaymentDate = on, Amount = amount };
        booking.Lines.Add(new BookingLine { Amount = amount, Pot = pot });
        return booking;
    }

    private static Booking Transfer(Guid from, Guid to, DateOnly on, decimal amount) => new()
    {
        AccountId = from, CounterAccountId = to, PaymentDate = on, Amount = amount,
    };
}
