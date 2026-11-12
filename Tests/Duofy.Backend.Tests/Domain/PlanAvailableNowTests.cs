using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>"Jetzt verfügbar" (Spec 139): Kontostände ohne Sparkonten minus offene Ausgaben des Monats.</summary>
public class PlanAvailableNowTests
{
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Tagesgeld = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> NonSavings = new HashSet<Guid> { Giro, Cash };

    private static PlanPosition Position(
        decimal amount, Pot pot = Pot.Needs, bool done = false, bool moved = false, bool passThrough = false, Guid? from = null, Guid? to = null) => new()
    {
        Id = Guid.NewGuid(),
        Label = "Posten",
        AmountPlanned = amount,
        DueDay = 15,
        Type = pot == Pot.Income ? CommitmentType.Income : CommitmentType.Contract,
        Pot = pot,
        AccountId = from,
        CounterAccountId = to,
        PassThrough = passThrough,
        DoneAt = done ? DateTimeOffset.UtcNow : null,
        MovedToPositionId = moved ? Guid.NewGuid() : null,
    };

    private static Dictionary<Guid, decimal> Booked(params (PlanPosition Position, decimal Amount)[] entries) =>
        entries.ToDictionary(e => e.Position.Id, e => e.Amount);

    // A1: Spec 139, Beispiel.
    [Fact]
    public void Example_of_the_spec_gives_190_euro()
    {
        var rent = Position(800m);
        var power = Position(100m);
        var internet = Position(30m, done: true);
        var salary = Position(2000m, Pot.Income);

        var figure = PlanFigures.AvailableNow([rent, power, internet, salary], Booked((power, 40m)), NonSavings, 1050m);

        Assert.Equal(1050m, figure.Balances);
        Assert.Equal(860m, figure.OpenExpenses);
        Assert.Equal(190m, figure.Value);
    }

    [Fact]
    public void Savings_accounts_are_not_part_of_the_balances_rule()
    {
        Assert.True(Account.CountsAsAvailableFor(AccountType.Checking));
        Assert.True(Account.CountsAsAvailableFor(AccountType.Cash));
        Assert.False(Account.CountsAsAvailableFor(AccountType.Savings));
    }

    [Fact]
    public void Planned_income_is_not_counted()
    {
        var figure = PlanFigures.AvailableNow([Position(2000m, Pot.Income)], Booked(), NonSavings, 100m);

        Assert.Equal(100m, figure.Value);
    }

    [Fact]
    public void A_booked_amount_above_the_plan_leaves_nothing_open()
    {
        var position = Position(50m);

        Assert.Equal(0m, PlanFigures.AvailableNow([position], Booked((position, 80m)), NonSavings, 10m).OpenExpenses);
    }

    [Fact]
    public void A_negative_result_stays_negative()
    {
        Assert.Equal(-150m, PlanFigures.AvailableNow([Position(200m)], Booked(), NonSavings, 50m).Value);
    }

    [Fact]
    public void Moved_and_pass_through_positions_are_not_open_expenses()
    {
        var figure = PlanFigures.AvailableNow([Position(300m, moved: true), Position(70m, passThrough: true)], Booked(), NonSavings, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    // A2
    [Fact]
    public void A_transfer_between_own_non_savings_accounts_is_no_expense()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: Giro, to: Cash)], Booked(), NonSavings, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    [Fact]
    public void A_transfer_to_a_savings_account_is_an_open_expense_until_ticked()
    {
        var open = Position(200m, Pot.Savings, from: Giro, to: Tagesgeld);
        var ticked = Position(200m, Pot.Savings, done: true, from: Giro, to: Tagesgeld);

        Assert.Equal(200m, PlanFigures.AvailableNow([open], Booked(), NonSavings, 500m).OpenExpenses);
        Assert.Equal(0m, PlanFigures.AvailableNow([ticked], Booked(), NonSavings, 500m).OpenExpenses);
    }
}
