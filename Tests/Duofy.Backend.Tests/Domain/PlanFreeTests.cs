using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Der Frei-Betrag eines Monats für die Plan-Übersicht (Spec 23): aus den geplanten Beträgen gerechnet, nichts doppelt.</summary>
public class PlanFreeTests
{
    private static PlanPosition Position(decimal amount, Pot pot, bool passThrough = false) => new()
    {
        Id = Guid.NewGuid(),
        Label = "Posten",
        AmountPlanned = amount,
        DueDay = 1,
        Type = pot == Pot.Income ? CommitmentType.Income : CommitmentType.Contract,
        Pot = pot,
        PassThrough = passThrough,
    };

    [Fact]
    public void Free_is_the_planned_income_minus_the_planned_expenses()
    {
        var positions = new[]
        {
            Position(3000m, Pot.Income),
            Position(890m, Pot.Needs),
            Position(100m, Pot.Wants),
            Position(200m, Pot.Savings),
        };

        Assert.Equal(1810m, PlanFigures.Free(positions, NoneBooked, carriedIn: 0m, Nothing));
    }

    [Fact]
    public void Pass_through_positions_count_neither_as_income_nor_as_expense()
    {
        var positions = new[]
        {
            Position(3000m, Pot.Income),
            Position(500m, Pot.Income, passThrough: true),
            Position(890m, Pot.Needs),
            Position(500m, Pot.Needs, passThrough: true),
        };

        Assert.Equal(2110m, PlanFigures.Free(positions, NoneBooked, carriedIn: 0m, Nothing));
    }

    [Fact]
    public void Free_may_be_negative_and_an_empty_month_has_nothing_free()
    {
        Assert.Equal(-50m, PlanFigures.Free([Position(950m, Pot.Needs), Position(900m, Pot.Income)], NoneBooked, carriedIn: 0m, Nothing));
        Assert.Equal(0m, PlanFigures.Free([], NoneBooked, carriedIn: 0m, Nothing));
    }

    // Toms Beispiel vom 1.11.2026: 2500 geplant rein, 2200 geplant raus.
    [Theory]
    [InlineData(230.00, 530.00)]
    [InlineData(-150.00, 150.00)]
    [InlineData(0.00, 300.00)]
    public void Free_contains_the_carry_over_from_the_previous_month(double carriedIn, double free)
    {
        var positions = new[] { Position(2500m, Pot.Income), Position(2200m, Pot.Needs) };

        Assert.Equal((decimal)free, PlanFigures.Free(positions, NoneBooked, (decimal)carriedIn, Nothing));
    }

    private static readonly Dictionary<Pot, decimal> Nothing = Enum.GetValues<Pot>().ToDictionary(pot => pot, _ => 0m);

    private static readonly Dictionary<Guid, decimal> NoneBooked = [];

    // Spec 93, A8 (R7): Jeder Posten zählt mit seinem geplanten Betrag, oder mit dem gebuchten, wenn mehr gebucht ist;
    // für Ausgaben und Einnahmen, offen oder abgehakt. Toms Beispiele vom 04.11.2026.
    [Fact]
    public void Each_position_counts_with_the_planned_amount_or_with_the_booked_amount_when_more_was_booked()
    {
        var salary = Position(2000m, Pot.Income);
        var rent = Position(1500m, Pot.Needs);
        var hp = Position(4.48m, Pot.Needs);
        var groceries = Position(600m, Pot.Needs);
        groceries.DoneAt = new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero);
        var childBenefit = Position(267m, Pot.Income);
        var positions = new[] { salary, rent, hp, groceries, childBenefit };
        Assert.Equal(162.52m, PlanFigures.Free(positions, NoneBooked, carriedIn: 0m, Nothing));

        var booked = new Dictionary<Guid, decimal> { [hp.Id] = 5.48m, [groceries.Id] = 426m, [childBenefit.Id] = 300m, [salary.Id] = 1900m };

        // 2000 + 300 − 1500 − 5,48 − 600: das Gehalt mit weniger Gebuchtem zählt den Plan, Lebensmittel abgehakt mit 426 zählt 600.
        Assert.Equal(194.52m, PlanFigures.Free(positions, booked, carriedIn: 0m, Nothing));
    }

    // Spec 93, A8 (R7): Durchlaufende Posten bleiben draußen, auch wenn mehr gebucht ist als geplant.
    [Fact]
    public void Pass_through_positions_stay_out_even_when_more_was_booked()
    {
        var through = Position(500m, Pot.Needs, passThrough: true);
        var throughIncome = Position(500m, Pot.Income, passThrough: true);
        var positions = new[] { Position(2000m, Pot.Income), through, throughIncome };
        var booked = new Dictionary<Guid, decimal> { [through.Id] = 800m, [throughIncome.Id] = 900m };

        Assert.Equal(2000m, PlanFigures.Free(positions, booked, carriedIn: 0m, Nothing));
    }

    // Spec 93, A1: 2000 geplant rein, 1500 geplant raus, 100 Lebensmittel ohne Posten.
    [Fact]
    public void Free_subtracts_the_booked_expenses_without_a_position()
    {
        var positions = new[] { Position(2000m, Pot.Income), Position(1500m, Pot.Needs) };
        var unplanned = new Dictionary<Pot, decimal>(Nothing) { [Pot.Needs] = 60m, [Pot.Wants] = 30m, [Pot.Savings] = 10m };

        Assert.Equal(400m, PlanFigures.Free(positions, NoneBooked, carriedIn: 0m, unplanned));
    }

    // Spec 93, A5: Eine Einnahme ohne Posten, etwa Zinsen, ist Geld, das der Plan nicht kannte.
    [Fact]
    public void Free_adds_the_booked_income_without_a_position_together_with_the_carry_over()
    {
        var positions = new[] { Position(2000m, Pot.Income), Position(1500m, Pot.Needs) };
        var unplanned = new Dictionary<Pot, decimal>(Nothing) { [Pot.Income] = 20m, [Pot.Needs] = 100m };

        Assert.Equal(650m, PlanFigures.Free(positions, NoneBooked, carriedIn: 230m, unplanned));
    }

    private static Booking Carried(decimal amount, int planMonth = 11) => new()
    {
        Id = Guid.NewGuid(),
        Amount = amount,
        PaymentDate = new DateOnly(2026, 10, 31),
        PlanYear = 2026,
        PlanMonth = planMonth,
    };

    private static Plan Plan(Guid? income = null, Guid? expense = null) => new()
    {
        Year = 2026,
        Month = 11,
        CarryOverIncomeBookingId = income,
        CarryOverExpenseBookingId = expense,
    };

    [Fact]
    public void The_carry_over_comes_from_the_income_or_as_a_negative_amount_from_the_expense_and_is_null_without_one()
    {
        var income = Carried(230m);
        var expense = Carried(150m);

        Assert.Equal(230m, PlanFigures.CarriedIn(Plan(income: income.Id), [income, expense]));
        Assert.Equal(-150m, PlanFigures.CarriedIn(Plan(expense: expense.Id), [income, expense]));
        Assert.Null(PlanFigures.CarriedIn(Plan(), [income, expense]));
        // Ohne Sicht auf die Buchungen gibt es keinen Betrag, auch wenn der Plan die Kennung kennt.
        Assert.Null(PlanFigures.CarriedIn(Plan(income: income.Id), []));
    }

    // Der Plan kennt beide Hälften des Paares; in den Monat zählt nur die mit seinem Planmonat, egal was die Liste sonst enthält.
    [Fact]
    public void Only_the_half_in_the_plan_month_counts_when_the_list_holds_both()
    {
        var negativeOut = Carried(150m, planMonth: 10);
        var negativeIn = Carried(150m);
        var positiveIn = Carried(230m);
        var positiveOut = Carried(230m, planMonth: 10);

        Assert.Equal(-150m, PlanFigures.CarriedIn(Plan(income: negativeOut.Id, expense: negativeIn.Id), [negativeOut, negativeIn]));
        Assert.Equal(230m, PlanFigures.CarriedIn(Plan(income: positiveIn.Id, expense: positiveOut.Id), [positiveOut, positiveIn]));
    }
}
