using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Zahlung auf eine Schuld (Issue 31): Die Eingaben werden zur Ausgabe mit Zuordnung.</summary>
public class DebtPaymentDraftTests
{
    private static readonly Guid Debt = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();
    private static readonly Guid Giro = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Fact]
    public void A_payment_is_an_expense_in_needs_by_default_with_the_debt_on_its_line()
    {
        var draft = DebtPaymentDraft.ForNew(Debt, "Stromrechnung", Category, Giro, Today);
        draft.Amount = 75.5m;

        var request = draft.ToRequest(owner: null);

        Assert.Equal((BookingKinds.Expense, Giro, null, Today, 75.5m), (request.Kind, request.AccountId, request.CounterAccountId, request.Date, request.Amount));
        var line = Assert.Single(request.Lines!);
        Assert.Equal((75.5m, PotKeys.Needs, Category, Debt), (line.Amount, line.Pot, line.CategoryId, line.DebtId));
        Assert.Contains("Stromrechnung", request.Note);
    }

    [Fact]
    public void The_pot_can_be_wants_and_the_owner_is_passed_on_but_savings_and_income_are_ignored()
    {
        var owner = Guid.NewGuid();
        var draft = DebtPaymentDraft.ForNew(Debt, "X", Category, Giro, Today);
        draft.Amount = 10m;

        draft.SetPot(PotKeys.Wants);
        draft.SetPot(PotKeys.Savings);
        draft.SetPot(PotKeys.Income);

        Assert.Equal(PotKeys.Wants, draft.Pot);
        Assert.Equal(owner, draft.ToRequest(owner).OwnerId);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    [InlineData(1.005, false)]
    [InlineData(0.01, true)]
    public void Only_a_positive_amount_with_at_most_two_decimals_is_valid(double? amount, bool valid)
    {
        var draft = DebtPaymentDraft.ForNew(Debt, "X", Category, Giro, Today);
        draft.Amount = amount is null ? null : (decimal)amount;

        Assert.Equal(valid, draft.IsAmountValid);
    }
}
