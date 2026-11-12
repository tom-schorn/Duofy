using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>"Jetzt verfügbar" (Spec 139): Kontostände der verfügbaren Konten (Schalter am Konto) minus offene Ausgaben des Monats.</summary>
public class PlanAvailableNowTests
{
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Tagesgeld = Guid.NewGuid();
    private static readonly Guid Spardose = Guid.NewGuid();
    // Verfügbar sind Giro (das Standardkonto) und Bargeld; Tagesgeld und Spardose sind es nicht.
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

        var figure = PlanFigures.AvailableNow([rent, power, internet, salary], Booked((power, 40m)), NonSavings, Giro, 1050m);

        Assert.Equal(1050m, figure.Balances);
        Assert.Equal(860m, figure.OpenExpenses);
        Assert.Equal(190m, figure.Value);
    }

    [Fact]
    public void Planned_income_is_not_counted()
    {
        var figure = PlanFigures.AvailableNow([Position(2000m, Pot.Income)], Booked(), NonSavings, Giro, 100m);

        Assert.Equal(100m, figure.Value);
    }

    [Fact]
    public void A_booked_amount_above_the_plan_leaves_nothing_open()
    {
        var position = Position(50m);

        Assert.Equal(0m, PlanFigures.AvailableNow([position], Booked((position, 80m)), NonSavings, Giro, 10m).OpenExpenses);
    }

    [Fact]
    public void A_negative_result_stays_negative()
    {
        Assert.Equal(-150m, PlanFigures.AvailableNow([Position(200m)], Booked(), NonSavings, Giro, 50m).Value);
    }

    [Fact]
    public void Moved_and_pass_through_positions_are_not_open_expenses()
    {
        var figure = PlanFigures.AvailableNow([Position(300m, moved: true), Position(70m, passThrough: true)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    // A2
    [Fact]
    public void A_transfer_between_own_non_savings_accounts_is_no_expense()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: Giro, to: Cash)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    [Fact]
    public void A_transfer_to_a_not_available_account_is_an_open_expense_until_ticked()
    {
        var open = Position(200m, Pot.Savings, from: Giro, to: Tagesgeld);
        var ticked = Position(200m, Pot.Savings, done: true, from: Giro, to: Tagesgeld);

        Assert.Equal(200m, PlanFigures.AvailableNow([open], Booked(), NonSavings, Giro, 500m).OpenExpenses);
        Assert.Equal(0m, PlanFigures.AvailableNow([ticked], Booked(), NonSavings, Giro, 500m).OpenExpenses);
    }

    // MUSS 1: Ohne Konto am Posten gilt das Standardkonto, wie beim Abhaken.
    [Fact]
    public void A_transfer_without_an_account_starts_at_the_default_account_and_stays_inside_the_available_accounts()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: null, to: Cash)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    [Fact]
    public void A_transfer_without_an_account_to_a_not_available_account_is_an_open_expense()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: null, to: Tagesgeld)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(200m, figure.OpenExpenses);
    }

    // SOLLTE 3: Geld, das von einem nicht verfügbaren Konto kommt, senkt die Zahl nicht.
    [Fact]
    public void A_transfer_from_a_not_available_account_into_an_available_one_lowers_nothing()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: Tagesgeld, to: Giro)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    [Fact]
    public void A_transfer_between_two_not_available_accounts_lowers_nothing()
    {
        var figure = PlanFigures.AvailableNow([Position(200m, Pot.Savings, from: Tagesgeld, to: Spardose)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(0m, figure.OpenExpenses);
    }

    [Fact]
    public void An_expense_paid_from_a_not_available_account_lowers_nothing()
    {
        var figure = PlanFigures.AvailableNow([Position(120m, from: Tagesgeld), Position(30m, from: Cash), Position(10m)], Booked(), NonSavings, Giro, 500m);

        Assert.Equal(40m, figure.OpenExpenses);
    }
}
