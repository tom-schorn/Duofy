using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Einzahlen und Entnehmen bei einem Sparziel (Issue 30): Die Eingaben werden zur passenden Umbuchung mit Zuordnung.</summary>
public class SavingsMoveDraftTests
{
    private static readonly Guid Goal = Guid.NewGuid();
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 24);

    private static SavingsMoveDraft Draft(SavingsMoveKind kind, decimal? amount = 100m) => SavingsMoveDraft.ForNew(kind, Goal, "Urlaub", Savings, Today) with
    {
        Amount = amount,
        OtherAccountId = Giro,
    };

    [Fact]
    public void A_deposit_moves_money_from_the_other_account_into_the_pot_savings_and_names_the_goal()
    {
        var request = Draft(SavingsMoveKind.Deposit).ToRequest(owner: null);

        Assert.Equal(BookingKinds.Transfer, request.Kind);
        Assert.Equal((Giro, Savings, Today, 100m), (request.AccountId, request.CounterAccountId, request.Date, request.Amount));
        Assert.Equal(PotKeys.Savings, request.TargetPot);
        var line = Assert.Single(request.Lines!);
        Assert.Equal((100m, PotKeys.Wants, Goal), (line.Amount, line.Pot, line.SavingsGoalId));
        Assert.Contains("Urlaub", request.Note);
    }

    [Fact]
    public void A_withdrawal_moves_money_from_the_pot_savings_to_the_other_account_and_names_the_goal()
    {
        var request = Draft(SavingsMoveKind.Withdrawal).ToRequest(owner: null);

        Assert.Equal((Savings, Giro), (request.AccountId, request.CounterAccountId));
        Assert.Equal(PotKeys.Wants, request.TargetPot);
        var line = Assert.Single(request.Lines!);
        Assert.Equal((PotKeys.Savings, Goal), (line.Pot, line.SavingsGoalId));
    }

    [Fact]
    public void The_pot_for_the_other_side_can_be_needs_or_wants_and_the_owner_is_passed_on()
    {
        var owner = Guid.NewGuid();
        var draft = Draft(SavingsMoveKind.Deposit);
        draft.SetPot(PotKeys.Needs);

        var request = draft.ToRequest(owner);

        Assert.Equal(PotKeys.Needs, Assert.Single(request.Lines!).Pot);
        Assert.Equal(owner, request.OwnerId);
    }

    [Fact]
    public void A_pot_outside_needs_and_wants_is_ignored()
    {
        var draft = Draft(SavingsMoveKind.Deposit);

        draft.SetPot(PotKeys.Savings);
        draft.SetPot(PotKeys.Income);

        Assert.Equal(PotKeys.Wants, draft.Pot);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.0, false)]
    [InlineData(-5.0, false)]
    [InlineData(10.005, false)]
    [InlineData(0.01, true)]
    [InlineData(250.0, true)]
    public void Only_a_positive_amount_with_at_most_two_decimals_is_valid(double? amount, bool valid)
    {
        Assert.Equal(valid, Draft(SavingsMoveKind.Deposit, amount is null ? null : (decimal)amount).IsAmountValid);
    }

    [Fact]
    public void Both_accounts_must_be_chosen_and_differ()
    {
        var draft = Draft(SavingsMoveKind.Deposit);
        Assert.True(draft.HasAccounts);

        Assert.False((draft with { OtherAccountId = null }).HasAccounts);
        Assert.False((draft with { SavingsAccountId = null }).HasAccounts);
        Assert.False((draft with { OtherAccountId = Savings }).HasAccounts);
    }
}
