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

        Assert.Equal(1810m, PlanFigures.Free(positions));
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

        Assert.Equal(2110m, PlanFigures.Free(positions));
    }

    [Fact]
    public void Free_may_be_negative_and_an_empty_month_has_nothing_free()
    {
        Assert.Equal(-50m, PlanFigures.Free([Position(950m, Pot.Needs), Position(900m, Pot.Income)]));
        Assert.Equal(0m, PlanFigures.Free([]));
    }
}
