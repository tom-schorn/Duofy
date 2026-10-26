using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Der Abgleich eines Monats mit den Verträgen von heute (R2): geänderte Felder, neue fällige
/// Verträge und Verträge, die nicht mehr fällig sind. Abgehakte und von Hand geänderte Posten
/// werden gemeldet, aber geschützt.
/// </summary>
public class PlanChangesTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void An_untouched_plan_has_no_changes()
    {
        var rent = Monthly("Miete", 890.00m);
        var plan = Plan.CreateFrom([rent], 2026, 11);

        Assert.Empty(PlanChanges.Compare(plan, [rent]));
    }

    [Fact]
    public void A_changed_amount_is_listed_with_before_and_after()
    {
        var rent = Monthly("Miete", 890.00m);
        var plan = Plan.CreateFrom([rent], 2026, 11);
        rent.Amount = 950.00m;

        var change = Assert.Single(PlanChanges.Compare(plan, [rent]));

        Assert.Equal(PlanChangeKind.Changed, change.Kind);
        Assert.Equal(plan.Positions[0].Id, change.PositionId);
        Assert.Equal(rent.Id, change.CommitmentId);
        Assert.False(change.Protected);
        var field = Assert.Single(change.Fields);
        Assert.Equal(PlanField.Amount, field.Field);
        Assert.Equal(890.00m, field.Before);
        Assert.Equal(950.00m, field.After);
    }

    [Fact]
    public void Applying_a_change_copies_the_contract_again()
    {
        var rent = Monthly("Miete", 890.00m);
        var plan = Plan.CreateFrom([rent], 2026, 11);
        rent.Amount = 950.00m;
        rent.Name = "Miete neu";

        var change = Assert.Single(PlanChanges.Compare(plan, [rent]));
        PlanChanges.Apply(plan, change, [rent]);

        Assert.Equal(950.00m, plan.Positions[0].AmountPlanned);
        Assert.Equal("Miete neu", plan.Positions[0].Label);
        Assert.Empty(PlanChanges.Compare(plan, [rent]));
    }

    [Fact]
    public void Ticked_and_hand_changed_positions_are_protected()
    {
        var rent = Monthly("Miete", 890.00m);
        var power = Monthly("Strom", 75.00m);
        var plan = Plan.CreateFrom([rent, power], 2026, 11);
        plan.Positions.Single(p => p.Label == "Miete").DoneAt = DateTimeOffset.UnixEpoch;
        plan.Positions.Single(p => p.Label == "Strom").ChangedByHand = true;
        rent.Amount = 950.00m;
        power.Amount = 80.00m;

        var changes = PlanChanges.Compare(plan, [rent, power]);

        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.True(c.Protected));
    }

    [Fact]
    public void A_new_due_contract_is_listed_and_applying_it_adds_a_position()
    {
        var rent = Monthly("Miete", 890.00m);
        var plan = Plan.CreateFrom([rent], 2026, 11);
        var gym = Monthly("Fitness", 30.00m);

        var change = Assert.Single(PlanChanges.Compare(plan, [rent, gym]));
        Assert.Equal(PlanChangeKind.New, change.Kind);
        Assert.Null(change.PositionId);
        Assert.Equal(gym.Id, change.CommitmentId);

        PlanChanges.Apply(plan, change, [rent, gym]);

        Assert.Contains(plan.Positions, p => p.CommitmentId == gym.Id && p.AmountPlanned == 30.00m);
        Assert.Empty(PlanChanges.Compare(plan, [rent, gym]));
    }

    [Fact]
    public void A_contract_no_longer_due_is_listed_and_applying_it_removes_the_position()
    {
        var gym = Monthly("Fitness", 30.00m);
        var plan = Plan.CreateFrom([gym], 2026, 11);
        gym.LastDueDate = new DateOnly(2026, 10, 1);

        var change = Assert.Single(PlanChanges.Compare(plan, [gym]));
        Assert.Equal(PlanChangeKind.NoLongerDue, change.Kind);

        PlanChanges.Apply(plan, change, [gym]);

        Assert.Empty(plan.Positions);
    }

    [Fact]
    public void One_off_positions_are_never_listed()
    {
        var plan = Plan.CreateFrom([], 2026, 11);
        plan.Positions.Add(new PlanPosition
        {
            Id = Guid.NewGuid(),
            Label = "Geschenk",
            AmountPlanned = 50.00m,
            DueDay = 20,
            Type = CommitmentType.Contract,
            Pot = Pot.Wants,
        });

        Assert.Empty(PlanChanges.Compare(plan, []));
    }

    // A24
    [Fact]
    public void A_month_before_the_first_account_opening_offers_no_new_contracts()
    {
        var rent = Monthly("Miete", 890.00m);
        rent.FirstDueDate = new DateOnly(2020, 1, 1);
        var september = Plan.CreateFrom([], 2026, 9);
        var october = Plan.CreateFrom([], 2026, 10);
        var firstOpening = new DateOnly(2026, 10, 1);

        Assert.Empty(PlanChanges.Compare(september, [rent], firstOpening));
        Assert.Equal(PlanChangeKind.New, Assert.Single(PlanChanges.Compare(october, [rent], firstOpening)).Kind);
    }

    private static Commitment Monthly(string name, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Amount = amount,
        Type = CommitmentType.Contract,
        OwnerId = Owner,
        IntervalMonths = 1,
        FirstDueDate = new DateOnly(2026, 1, 1),
        Pot = Pot.Needs,
    };
}
