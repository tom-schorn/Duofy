using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Ein verschobener Posten (Spec 136) zählt in dem Monat, den er verlassen hat, nirgends mehr; seine Kopie
/// zählt im nächsten Monat wie jeder andere Posten. Je Zahl ein Test (Risiko: eine Zahl, die das Merkmal
/// vergisst, zählt eine Zahlung doppelt).
/// </summary>
public class PlanPostponeFiguresTests
{
    private static readonly Dictionary<Pot, decimal> Nothing = Enum.GetValues<Pot>().ToDictionary(p => p, _ => 0m);
    private static readonly Dictionary<Guid, decimal> NoneBooked = new();

    private static PlanPosition Position(decimal amount, Pot pot, bool moved = false) => new()
    {
        Id = Guid.NewGuid(),
        Label = "Posten",
        AmountPlanned = amount,
        DueDay = 1,
        Type = pot == Pot.Income ? CommitmentType.Income : CommitmentType.Contract,
        Pot = pot,
        MovedToPositionId = moved ? Guid.NewGuid() : null,
    };

    [Fact]
    public void Free_leaves_a_moved_position_out_and_counts_the_copy()
    {
        PlanPosition[] november = [Position(3000m, Pot.Income), Position(890m, Pot.Needs), Position(4m, Pot.Wants, moved: true)];
        PlanPosition[] december = [Position(3000m, Pot.Income), Position(4m, Pot.Wants)];

        Assert.Equal(2110m, PlanFigures.Free(november, NoneBooked, 0m, Nothing, 0m));
        Assert.Equal(2996m, PlanFigures.Free(december, NoneBooked, 0m, Nothing, 0m));
    }

    [Fact]
    public void Free_counts_no_booked_amount_of_a_moved_position()
    {
        var moved = Position(4m, Pot.Wants, moved: true);

        Assert.Equal(0m, PlanFigures.Free([moved], new Dictionary<Guid, decimal> { [moved.Id] = 9m }, 0m, Nothing, 0m));
    }

    [Fact]
    public void Income_leaves_a_moved_income_position_out()
    {
        Assert.Equal(3000m, PlanFigures.Income([Position(3000m, Pot.Income), Position(500m, Pot.Income, moved: true)]));
    }

    [Fact]
    public void Fixed_leaves_a_moved_position_out()
    {
        Assert.Equal(890m, PlanFigures.Fixed([Position(890m, Pot.Needs), Position(4m, Pot.Wants, moved: true)]));
    }

    [Fact]
    public void Pot_totals_leave_a_moved_position_out_planned_and_booked()
    {
        var moved = Position(4m, Pot.Wants, moved: true);
        var kept = Position(10m, Pot.Wants);
        var booked = new Dictionary<Guid, decimal> { [moved.Id] = 4m, [kept.Id] = 2m };

        var wants = PlanFigures.PotTotals([moved, kept], booked, Nothing, Nothing)[Pot.Wants];

        Assert.Equal(new PotTotal(10m, 2m), wants);
    }

    [Fact]
    public void Quota_actual_leaves_a_moved_position_out()
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        var salary = Position(1000m, Pot.Income);
        var moved = Position(100m, Pot.Wants, moved: true);
        var kept = Position(100m, Pot.Wants);
        plan.Positions.AddRange([salary, moved, kept]);
        Booking Line(decimal amount, Pot pot, Guid id) => new()
        {
            Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), PaymentDate = new DateOnly(2026, 11, 5), Amount = amount,
            Lines = [new BookingLine { Amount = amount, Pot = pot, PositionId = id }],
        };

        var actual = PlanFigures.QuotaActual(plan, [Line(1000m, Pot.Income, salary.Id), Line(100m, Pot.Wants, moved.Id), Line(100m, Pot.Wants, kept.Id)], null);

        Assert.Equal(10.0m, actual[Pot.Wants]);
    }

    [Fact]
    public void The_household_sum_leaves_a_moved_position_out_of_income_pots_and_unpaid()
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        plan.OwnerId = Guid.NewGuid();
        plan.Positions.AddRange([Position(3000m, Pot.Income), Position(200m, Pot.Needs), Position(50m, Pot.Needs, moved: true), Position(70m, Pot.Income, moved: true)]);

        var figures = HouseholdPlanFigures.Compute([new MemberMonth(plan, [], [])]);

        Assert.Equal(3000m, figures.Income);
        Assert.Equal(200m, figures.Pots[Pot.Needs].Planned);
        Assert.Equal(200m, figures.Unpaid);
    }

    [Fact]
    public void Postponing_copies_the_position_with_hand_flag_and_clamps_the_due_day()
    {
        var position = new PlanPosition
        {
            Id = Guid.NewGuid(), CommitmentId = Guid.NewGuid(), Label = "GitHub", AmountPlanned = 4m, DueDay = 31, Type = CommitmentType.Contract,
            CategoryId = Guid.NewGuid(), Pot = Pot.Wants, AccountId = Guid.NewGuid(), PaymentMethod = PaymentMethod.StandingOrder,
        };

        var copy = position.PostponedCopy(2026, 11);

        Assert.NotEqual(position.Id, copy.Id);
        Assert.Equal(position.Id, copy.MovedFromPositionId);
        Assert.Equal(copy.Id, position.MovedToPositionId);
        Assert.True(copy.ChangedByHand);
        Assert.Equal(30, copy.DueDay);
        Assert.Equal((position.CommitmentId, position.Label, position.AmountPlanned, position.CategoryId, position.Pot, position.AccountId, position.PaymentMethod),
            (copy.CommitmentId, copy.Label, copy.AmountPlanned, copy.CategoryId, copy.Pot, copy.AccountId, copy.PaymentMethod));
    }

    /// <summary>Eine Kopie aus dem November, wie sie das Verschieben anlegt; der Dezember-Posten selbst bleibt unverschoben.</summary>
    private static PlanPosition CopyFromNovember(PlanPosition like)
    {
        var november = new PlanPosition { Id = Guid.NewGuid(), CommitmentId = like.CommitmentId, Label = like.Label, AmountPlanned = like.AmountPlanned, DueDay = like.DueDay, Type = like.Type, Pot = like.Pot };
        return november.PostponedCopy(2026, 12);
    }

    private static Commitment Monthly(decimal amount) => new()
    {
        Id = Guid.NewGuid(), Name = "GitHub", Amount = amount, Type = CommitmentType.Contract, OwnerId = Guid.NewGuid(),
        IntervalMonths = 1, FirstDueDate = new DateOnly(2026, 1, 1), Pot = Pot.Wants,
    };

    [Fact]
    public void The_change_notice_never_matches_or_lists_a_copy_and_a_contract_change_leaves_it()
    {
        var github = Monthly(4m);
        var december = Plan.CreateFrom([github], 2026, 12);
        var regular = december.Positions[0];
        var copy = CopyFromNovember(regular);
        december.Positions.Add(copy);
        github.Amount = 5m;

        var change = Assert.Single(PlanChanges.Compare(december, [github]));
        PlanChanges.Apply(december, change, [github]);

        Assert.Equal(regular.Id, change.PositionId);
        Assert.Equal(5m, regular.AmountPlanned);
        Assert.Equal(4m, copy.AmountPlanned);
    }

    [Fact]
    public void An_ended_contract_keeps_the_copy_and_removes_only_the_regular_position()
    {
        var github = Monthly(4m);
        var december = Plan.CreateFrom([github], 2026, 12);
        var copy = CopyFromNovember(december.Positions[0]);
        december.Positions.Add(copy);
        github.LastDueDate = new DateOnly(2026, 11, 30);

        var changes = PlanChanges.Compare(december, [github]);
        var gone = Assert.Single(changes);
        PlanChanges.Apply(december, gone, [github]);

        Assert.Equal(PlanChangeKind.NoLongerDue, gone.Kind);
        Assert.NotEqual(copy.Id, gone.PositionId);
        Assert.Equal(copy.Id, Assert.Single(december.Positions).Id);
    }

    [Fact]
    public void A_copy_alone_does_not_count_as_the_planned_contract()
    {
        var github = Monthly(4m);
        var december = Plan.CreateFrom([], 2026, 12);
        var copy = new PlanPosition { Id = Guid.NewGuid(), CommitmentId = github.Id, Label = "GitHub", AmountPlanned = 4m, DueDay = 1, Type = CommitmentType.Contract, MovedFromPositionId = Guid.NewGuid() };
        december.Positions.Add(copy);

        var change = Assert.Single(PlanChanges.Compare(december, [github]));

        Assert.Equal(PlanChangeKind.New, change.Kind);
    }
}
