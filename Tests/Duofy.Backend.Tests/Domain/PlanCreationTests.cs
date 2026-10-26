using Duofy.Domain;

namespace Duofy.Domain.Tests;

public class PlanCreationTests
{
    // Alle Beispielverträge gehören demselben ausgedachten Benutzer.
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Plan_carries_the_year_and_month_it_was_created_for()
    {
        var plan = Plan.CreateFrom([], 2026, 9);

        Assert.Equal(2026, plan.Year);
        Assert.Equal(9, plan.Month);
    }

    [Fact]
    public void Without_commitments_the_plan_is_empty()
    {
        var plan = Plan.CreateFrom([], 2026, 9);

        Assert.Empty(plan.Positions);
    }

    [Fact]
    public void Due_commitment_becomes_a_position()
    {
        var rent = Monthly("Rent", 890.00m, firstDue: new DateOnly(2026, 1, 1));

        var plan = Plan.CreateFrom([rent], 2026, 9);

        // Assert.Single prüft, dass genau ein Posten da ist, und gibt ihn zurück.
        var position = Assert.Single(plan.Positions);
        Assert.Equal("Rent", position.Label);
        Assert.Equal(890.00m, position.AmountPlanned);
        Assert.Equal(1, position.DueDay);
        Assert.Equal(CommitmentType.Contract, position.Type);
    }

    [Fact]
    public void Type_of_the_commitment_is_copied_to_the_position()
    {
        var salary = Monthly("Salary", 2400.00m, firstDue: new DateOnly(2026, 1, 28));
        salary.Type = CommitmentType.Income;

        var plan = Plan.CreateFrom([salary], 2026, 9);

        var position = Assert.Single(plan.Positions);
        Assert.Equal(CommitmentType.Income, position.Type);
    }

    [Fact]
    public void Commitment_not_due_this_month_is_left_out()
    {
        // Alle drei Monate ab Januar heißt Januar, April, Juli, Oktober. Nicht September.
        var insurance = Monthly("Insurance", 120.00m, firstDue: new DateOnly(2026, 1, 15));
        insurance.IntervalMonths = 3;

        var plan = Plan.CreateFrom([insurance], 2026, 9);

        Assert.Empty(plan.Positions);
    }

    [Fact]
    public void Commitment_before_its_first_payment_is_left_out()
    {
        var gym = Monthly("Gym", 30.00m, firstDue: new DateOnly(2026, 10, 1));

        var plan = Plan.CreateFrom([gym], 2026, 9);

        Assert.Empty(plan.Positions);
    }

    [Fact]
    public void Commitment_after_its_last_payment_is_left_out()
    {
        var gym = Monthly("Gym", 30.00m, firstDue: new DateOnly(2026, 1, 1));
        gym.LastDueDate = new DateOnly(2026, 8, 1);

        var plan = Plan.CreateFrom([gym], 2026, 9);

        Assert.Empty(plan.Positions);
    }

    [Fact]
    public void Due_day_is_shortened_to_the_length_of_the_month()
    {
        // Der September hat 30 Tage, also wird aus Zahltag 31 der 30.
        var loan = Monthly("Loan", 250.00m, firstDue: new DateOnly(2026, 1, 31));

        var plan = Plan.CreateFrom([loan], 2026, 9);

        var position = Assert.Single(plan.Positions);
        Assert.Equal(30, position.DueDay);
    }

    [Fact]
    public void Position_is_a_copy_and_ignores_later_changes_to_the_commitment()
    {
        var rent = Monthly("Rent", 890.00m, firstDue: new DateOnly(2026, 1, 1));
        var plan = Plan.CreateFrom([rent], 2026, 9);

        // Die Miete wird erhöht und umbenannt, nachdem der September angelegt wurde.
        rent.Amount = 950.00m;
        rent.Name = "Rent (new)";

        var position = Assert.Single(plan.Positions);
        Assert.Equal(890.00m, position.AmountPlanned);
        Assert.Equal("Rent", position.Label);
    }

    [Fact]
    public void Only_the_due_commitments_of_several_end_up_in_the_plan()
    {
        var rent = Monthly("Rent", 890.00m, firstDue: new DateOnly(2026, 1, 1));
        var notYet = Monthly("Gym", 30.00m, firstDue: new DateOnly(2026, 10, 1));
        var power = Monthly("Power", 75.00m, firstDue: new DateOnly(2026, 1, 15));

        var plan = Plan.CreateFrom([rent, notYet, power], 2026, 9);

        Assert.Equal(2, plan.Positions.Count);
    }

    [Fact]
    public void Positions_are_sorted_by_due_day()
    {
        // Absichtlich durcheinander übergeben: 28., 1., 15.
        var salary = Monthly("Salary", 2400.00m, firstDue: new DateOnly(2026, 1, 28));
        var rent = Monthly("Rent", 890.00m, firstDue: new DateOnly(2026, 1, 1));
        var power = Monthly("Power", 75.00m, firstDue: new DateOnly(2026, 1, 15));

        var plan = Plan.CreateFrom([salary, rent, power], 2026, 9);

        Assert.Equal(3, plan.Positions.Count);
        Assert.Equal("Rent", plan.Positions[0].Label);
        Assert.Equal("Power", plan.Positions[1].Label);
        Assert.Equal("Salary", plan.Positions[2].Label);
    }

    [Fact]
    public void Month_zero_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan.CreateFrom([], 2026, 0));
    }

    [Fact]
    public void Month_thirteen_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan.CreateFrom([], 2026, 13));
    }

    // Baut einen Vertrag, der jeden Monat fällig ist. Einzelne Tests ändern
    // danach nur das Feld, um das es ihnen geht.
    private static Commitment Monthly(string name, decimal amount, DateOnly firstDue)
    {
        return new Commitment
        {
            Name = name,
            Amount = amount,
            Type = CommitmentType.Contract,
            OwnerId = Owner,
            IntervalMonths = 1,
            FirstDueDate = firstDue,
        };
    }
}
