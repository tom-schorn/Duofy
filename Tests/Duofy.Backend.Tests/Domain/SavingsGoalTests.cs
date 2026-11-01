using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Sparziele (Issue 30): Der Stand ist nie gespeichert, sondern die Summe der Buchungszeilen, die dem Ziel zugeordnet
/// sind. Einzahlungen zählen plus, Entnahmen minus. Mit fester Rechnung, ohne Uhr und ohne Datenbank.
/// </summary>
public class SavingsGoalTests
{
    [Fact]
    public void An_expense_in_the_pot_savings_is_a_deposit()
    {
        Assert.Equal(50m, SavingsFlow.EffectOf(isTransfer: false, targetPot: null, linePot: Pot.Savings, amount: 50m));
    }

    [Theory]
    [InlineData(Pot.Needs)]
    [InlineData(Pot.Wants)]
    [InlineData(Pot.Income)]
    public void An_expense_or_income_outside_savings_cannot_belong_to_a_goal(Pot pot)
    {
        Assert.Null(SavingsFlow.EffectOf(isTransfer: false, targetPot: null, linePot: pot, amount: 50m));
    }

    [Theory]
    [InlineData(Pot.Needs)]
    [InlineData(Pot.Wants)]
    public void A_transfer_into_the_pot_savings_is_a_deposit(Pot source)
    {
        Assert.Equal(120m, SavingsFlow.EffectOf(isTransfer: true, targetPot: Pot.Savings, linePot: source, amount: 120m));
    }

    [Theory]
    [InlineData(Pot.Needs)]
    [InlineData(Pot.Wants)]
    public void A_transfer_out_of_the_pot_savings_is_a_withdrawal(Pot target)
    {
        Assert.Equal(-80m, SavingsFlow.EffectOf(isTransfer: true, targetPot: target, linePot: Pot.Savings, amount: 80m));
    }

    [Fact]
    public void A_transfer_between_two_savings_accounts_or_without_a_target_pot_moves_no_goal()
    {
        Assert.Null(SavingsFlow.EffectOf(isTransfer: true, targetPot: Pot.Savings, linePot: Pot.Savings, amount: 10m));
        Assert.Null(SavingsFlow.EffectOf(isTransfer: true, targetPot: null, linePot: Pot.Savings, amount: 10m));
        Assert.Null(SavingsFlow.EffectOf(isTransfer: true, targetPot: Pot.Wants, linePot: Pot.Needs, amount: 10m));
    }

    [Fact]
    public void The_balance_is_the_sum_of_deposits_minus_withdrawals()
    {
        Assert.Equal(70m, SavingsFlow.BalanceOf([100m, 50m, -80m]));
        Assert.Equal(0m, SavingsFlow.BalanceOf([]));
    }

    [Theory]
    [InlineData(1000, 999.99, false)]
    [InlineData(1000, 1000, true)]
    [InlineData(1000, 1200, true)]
    public void A_goal_with_a_target_amount_is_reached_at_or_above_it(double target, double balance, bool expected)
    {
        var goal = new SavingsGoal { Name = "Urlaub", TargetAmount = (decimal)target };

        Assert.Equal(expected, goal.IsReached((decimal)balance));
    }

    [Fact]
    public void A_goal_without_a_target_amount_is_never_reached()
    {
        Assert.False(new SavingsGoal { Name = "Notgroschen" }.IsReached(1_000_000m));
    }
}
