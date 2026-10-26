using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Was ein Posten seit Spec 66 aus dem Vertrag mitnimmt (R1): Kategorie, Topf, Konten,
/// durchlaufender Posten, Zahlungsart und die Kennung des Vertrags.
/// </summary>
public class PlanPositionCopyTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Rent = Guid.NewGuid();
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();

    [Fact]
    public void Position_copies_all_planning_fields_of_the_commitment()
    {
        var commitment = Monthly("Miete", 890.00m, new DateOnly(2026, 1, 1));
        commitment.CategoryId = Rent;
        commitment.Pot = Pot.Needs;
        commitment.AccountId = Giro;
        commitment.CounterAccountId = Savings;
        commitment.PassThrough = true;
        commitment.PaymentMethod = PaymentMethod.StandingOrder;

        var position = Assert.Single(Plan.CreateFrom([commitment], 2026, 11).Positions);

        Assert.Equal(commitment.Id, position.CommitmentId);
        Assert.Equal(Rent, position.CategoryId);
        Assert.Equal(Pot.Needs, position.Pot);
        Assert.Equal(Giro, position.AccountId);
        Assert.Equal(Savings, position.CounterAccountId);
        Assert.True(position.PassThrough);
        Assert.Equal(PaymentMethod.StandingOrder, position.PaymentMethod);
        Assert.False(position.IsDone);
        Assert.False(position.ChangedByHand);
    }

    [Fact]
    public void Positions_on_the_same_day_are_sorted_by_name()
    {
        var power = Monthly("Strom", 75.00m, new DateOnly(2026, 1, 1));
        var rent = Monthly("Miete", 890.00m, new DateOnly(2026, 1, 1));

        var plan = Plan.CreateFrom([power, rent], 2026, 11);

        Assert.Equal(["Miete", "Strom"], plan.Positions.Select(p => p.Label));
    }

    [Fact]
    public void A_new_plan_carries_the_default_quotas()
    {
        var plan = Plan.CreateFrom([], 2026, 11);

        Assert.Equal(new Quotas(50, 30, 20), plan.Quotas);
    }

    private static Commitment Monthly(string name, decimal amount, DateOnly firstDue) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Amount = amount,
        Type = CommitmentType.Contract,
        OwnerId = Owner,
        IntervalMonths = 1,
        FirstDueDate = firstDue,
    };
}
