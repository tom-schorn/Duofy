using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>Die Zahlen der Monatsübersicht (Spec 24): Einnahmen, Festes, Frei und die Töpfe, aus den Posten gerechnet, nichts doppelt.</summary>
public class PlanOverviewFiguresTests
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
    public void Income_and_fixed_expenses_leave_the_free_amount()
    {
        var positions = new[] { Position(3000m, Pot.Income), Position(1000m, Pot.Needs), Position(200m, Pot.Savings) };

        Assert.Equal(3000m, PlanFigures.Income(positions));
        Assert.Equal(1200m, PlanFigures.Fixed(positions));
        Assert.Equal(1800m, PlanFigures.Free(positions, carriedIn: 0m));
    }

    [Fact]
    public void Pass_through_positions_count_in_none_of_the_three()
    {
        var positions = new[]
        {
            Position(3000m, Pot.Income),
            Position(500m, Pot.Income, passThrough: true),
            Position(1200m, Pot.Needs),
            Position(500m, Pot.Needs, passThrough: true),
        };

        Assert.Equal(3000m, PlanFigures.Income(positions));
        Assert.Equal(1200m, PlanFigures.Fixed(positions));
    }

    [Fact]
    public void Pot_totals_hold_planned_and_booked_per_pot_without_pass_through()
    {
        var rent = Position(1000m, Pot.Needs);
        var passThrough = Position(300m, Pot.Needs, passThrough: true);
        var fun = Position(100m, Pot.Wants);
        var booked = new Dictionary<Guid, decimal> { [rent.Id] = 1200m, [passThrough.Id] = 300m };

        var totals = PlanFigures.PotTotals([rent, passThrough, fun], booked, new Dictionary<Pot, decimal>());

        Assert.Equal(new PotTotal(1000m, 1200m), totals[Pot.Needs]);
        Assert.Equal(new PotTotal(100m, 0m), totals[Pot.Wants]);
        Assert.Equal(new PotTotal(0m, 0m), totals[Pot.Savings]);
    }

    // Spec 72, A3
    [Fact]
    public void Pot_totals_add_what_is_booked_on_positions_and_what_is_unplanned()
    {
        var rent = Position(1000m, Pot.Needs);
        var booked = new Dictionary<Guid, decimal> { [rent.Id] = 100m };
        var unplanned = new Dictionary<Pot, decimal> { [Pot.Needs] = 40m, [Pot.Income] = 25m };

        var totals = PlanFigures.PotTotals([rent], booked, unplanned);

        Assert.Equal(new PotTotal(1000m, 140m), totals[Pot.Needs]);
        Assert.False(totals.ContainsKey(Pot.Income));
    }
}
