using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Die Regeln des Vertragsdialogs, ohne Oberfläche geprüft (Spec 64, R2, R4, R7, R8).</summary>
public class CommitmentDraftTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static CommitmentResponse Saved(string type = CommitmentTypes.Contract, string pot = PotKeys.Needs) => new(
        Guid.NewGuid(), type, "Miete", 890m, 1, new DateOnly(2026, 1, 1), null, FakeCategoryApi.Groceries.Id, pot,
        null, null, false, PaymentMethods.StandingOrder, null, null, new DateOnly(2026, 11, 1), true);

    [Fact]
    public void A_new_draft_is_a_monthly_contract_starting_today_in_the_pot_needs()
    {
        var draft = CommitmentDraft.ForNew(Today);

        Assert.Equal(CommitmentTypes.Contract, draft.Type);
        Assert.Equal(1, draft.IntervalMonths);
        Assert.Equal(Today, draft.FirstDueDate);
        Assert.Equal(PotKeys.Needs, draft.Pot);
        Assert.False(draft.PotIsFixed);
        Assert.Equal([PotKeys.Needs, PotKeys.Wants], draft.OfferedPots);
    }

    // A15 (Regel): Sparziel heißt Topf Sparen, fest, und Zielfelder.
    [Fact]
    public void A_savings_goal_fixes_the_pot_savings_and_shows_the_target_fields()
    {
        var draft = CommitmentDraft.ForNew(Today);

        draft.SetType(CommitmentTypes.SavingsGoal);

        Assert.Equal(PotKeys.Savings, draft.Pot);
        Assert.True(draft.PotIsFixed);
        Assert.True(draft.ShowsTarget);
    }

    [Fact]
    public void Back_to_contract_the_pot_is_free_again_and_the_target_fields_are_emptied()
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.SetType(CommitmentTypes.SavingsGoal);
        draft.TargetAmount = 3000m;
        draft.TargetDate = new DateOnly(2027, 7, 31);

        draft.SetType(CommitmentTypes.Contract);

        Assert.Equal(PotKeys.Needs, draft.Pot);
        Assert.False(draft.PotIsFixed);
        Assert.False(draft.ShowsTarget);
        Assert.Null(draft.TargetAmount);
        Assert.Null(draft.TargetDate);
    }

    [Fact]
    public void An_income_fixes_the_pot_income_and_drops_the_target_account()
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.CounterAccountId = Guid.NewGuid();

        draft.SetType(CommitmentTypes.Income);

        Assert.Equal(PotKeys.Income, draft.Pot);
        Assert.False(draft.OffersTargetAccount);
        Assert.Null(draft.CounterAccountId);
    }

    [Fact]
    public void Switching_between_income_and_spending_drops_the_category()
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.CategoryId = FakeCategoryApi.Groceries.Id;

        draft.SetType(CommitmentTypes.Income);

        Assert.Null(draft.CategoryId);
        Assert.True(draft.WantsIncomeCategories);
    }

    [Fact]
    public void Choosing_a_category_of_a_contract_takes_its_suggested_pot()
    {
        var draft = CommitmentDraft.ForNew(Today);

        draft.ChooseCategory(FakeCategoryApi.Restaurant);

        Assert.Equal(FakeCategoryApi.Restaurant.Id, draft.CategoryId);
        Assert.Equal(PotKeys.Wants, draft.Pot);
    }

    [Fact]
    public void A_new_draft_becomes_a_create_request_with_its_keys()
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.Name = " Miete ";
        draft.Amount = 890m;
        draft.ChooseCategory(FakeCategoryApi.Groceries);
        draft.PaymentMethod = PaymentMethods.StandingOrder;

        var request = draft.ToCreateRequest();

        Assert.Equal(new CreateCommitmentRequest(
            CommitmentTypes.Contract, " Miete ", 890m, 1, Today, null, FakeCategoryApi.Groceries.Id, PotKeys.Needs,
            null, null, false, PaymentMethods.StandingOrder, null, null), request);
    }

    // R8: Die letzte Zahlung lässt sich im Dialog leeren.
    [Fact]
    public void Emptying_fields_of_a_saved_contract_sends_the_clear_switches()
    {
        var saved = Saved(CommitmentTypes.SavingsGoal, PotKeys.Savings) with
        {
            LastDueDate = new DateOnly(2026, 12, 1),
            AccountId = Guid.NewGuid(),
            CounterAccountId = Guid.NewGuid(),
            TargetAmount = 3000m,
        };
        var draft = CommitmentDraft.From(saved);

        draft.LastDueDate = null;
        draft.AccountId = null;
        draft.CounterAccountId = null;
        draft.TargetAmount = null;

        var request = draft.ToUpdateRequest(saved);
        Assert.True(request.ClearLastDueDate);
        Assert.True(request.ClearAccountId);
        Assert.True(request.ClearCounterAccountId);
        Assert.True(request.ClearTarget);
    }

    [Fact]
    public void Unchanged_empty_fields_send_no_clear_switches()
    {
        var saved = Saved();

        var request = CommitmentDraft.From(saved).ToUpdateRequest(saved);

        Assert.Null(request.ClearLastDueDate);
        Assert.Null(request.ClearAccountId);
        Assert.Null(request.ClearCounterAccountId);
        Assert.Null(request.ClearTarget);
        Assert.Equal("Miete", request.Name);
        Assert.Equal(PaymentMethods.StandingOrder, request.PaymentMethod);
    }

    // Spec 17, A4 (Regel): Angeboten werden nur noch die klassischen Arten, Sparziel und Schuld nicht mehr.
    [Fact]
    public void Only_spending_and_income_are_offered()
    {
        Assert.Equal([CommitmentTypes.Contract, CommitmentTypes.Income], CommitmentDraft.OfferedTypes);
        Assert.True(CommitmentDraft.ForNew(Today).IsOfferedType);
        Assert.False(CommitmentDraft.From(Saved(CommitmentTypes.SavingsGoal, PotKeys.Savings)).IsOfferedType);
    }

    // Spec 17, A2 (Regel): Der nächste Termin richtet sich nach erster Zahlung, Abstand und heute.
    [Theory]
    [InlineData("2026-01-15", 1, null, "2026-11-15")]
    [InlineData("2026-10-21", 1, null, "2026-10-21")]
    [InlineData("2026-02-01", 3, null, "2026-11-01")]
    [InlineData("2027-03-01", 12, null, "2027-03-01")]
    [InlineData("2026-01-31", 1, null, "2026-10-31")]
    [InlineData("2026-08-31", 6, null, "2027-02-28")]
    [InlineData("2026-01-01", 1, "2026-10-01", null)]
    [InlineData("2026-01-01", 1, "2026-11-01", "2026-11-01")]
    public void The_next_due_date_follows_start_interval_and_end(string first, int interval, string? last, string? expected)
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.FirstDueDate = DateOnly.Parse(first);
        draft.IntervalMonths = interval;
        draft.LastDueDate = last is null ? null : DateOnly.Parse(last);

        Assert.Equal(expected is null ? null : DateOnly.Parse(expected), draft.NextDueDate(Today));
    }

    // Spec 17, A2 (Regel): Ohne gültigen Abstand gibt es keinen Termin.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(121)]
    public void Without_a_valid_interval_there_is_no_next_due_date(int? interval)
    {
        var draft = CommitmentDraft.ForNew(Today);
        draft.IntervalMonths = interval;

        Assert.Null(draft.NextDueDate(Today));
        Assert.False(draft.HasValidInterval);
    }

    // Spec 17, A2 (Regel): Ein Ende vor dem Start ist ein Fehler, ein Ende am selben Tag nicht.
    [Fact]
    public void An_end_before_the_start_is_recognised()
    {
        var draft = CommitmentDraft.ForNew(Today);

        draft.LastDueDate = Today.AddDays(-1);
        Assert.True(draft.EndsBeforeStart);

        draft.LastDueDate = Today;
        Assert.False(draft.EndsBeforeStart);

        draft.LastDueDate = null;
        Assert.False(draft.EndsBeforeStart);
    }
}
