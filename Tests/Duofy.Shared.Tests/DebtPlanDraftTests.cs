using Duofy.Schemas;
using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Der Entwurf des Ratenplans im Schulden-Dialog (Issue 137): Vorbelegung, Vorschau und Anfrage.</summary>
public class DebtPlanDraftTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static DebtResponse WithPlan() => new(
        Guid.NewGuid(), DebtKinds.Claim, "Ausgleich", "Verwalter", 786.94m, null, null, 400m, 386.94m, false,
        Plan: new DebtPlanResponse(Guid.NewGuid(), true, 50m, 1, new DateOnly(2026, 12, 1), 36.94m, Guid.NewGuid(), PaymentMethods.DirectDebit, Guid.NewGuid()));

    [Fact]
    public void A_new_draft_is_off_monthly_and_starts_on_the_first_of_next_month()
    {
        var draft = DebtPlanDraft.ForNew(Today);

        Assert.False(draft.Enabled);
        Assert.Equal((1, new DateOnly(2026, 12, 1)), (draft.IntervalMonths, draft.FirstDueDate));
        Assert.Null(draft.ToRequest());
    }

    [Fact]
    public void The_preview_of_the_real_example_shows_eight_installments_and_a_last_one_of_36_94()
    {
        var draft = DebtPlanDraft.ForNew(Today);
        draft.Enabled = true;
        draft.Rate = 50m;

        var preview = draft.Preview(386.94m);

        Assert.Equal((8, 36.94m, new DateOnly(2027, 7, 1)), (preview.Count, preview.LastAmount, preview.LastDueDate));
        Assert.Equal(InstallmentPlanError.None, preview.Error);
    }

    [Fact]
    public void Without_a_rate_there_is_no_preview_error_message_but_an_invalid_result()
    {
        var draft = DebtPlanDraft.ForNew(Today);
        draft.Enabled = true;

        Assert.NotEqual(InstallmentPlanError.None, draft.Preview(100m).Error);
    }

    [Fact]
    public void An_existing_plan_is_loaded_and_a_change_asks_for_the_month()
    {
        var draft = DebtPlanDraft.From(WithPlan(), Today);

        Assert.True(draft.Enabled);
        Assert.True(draft.HasOwnPlan);
        Assert.Equal((50m, 36.94m), (draft.Rate, draft.LastAmount));
        Assert.Equal(new DateOnly(2026, 12, 1), draft.FromMonth);
    }

    [Fact]
    public void The_request_for_a_change_carries_the_month_and_removing_clears_the_plan()
    {
        var draft = DebtPlanDraft.From(WithPlan(), Today);
        Assert.Null(draft.ToRequest());
        draft.Rate = 100m;
        draft.FromMonth = new DateOnly(2027, 3, 15);

        var request = draft.ToRequest()!;

        Assert.Equal((100m, new DateOnly(2027, 3, 1)), (request.Rate, request.FromMonth));
        draft.Remove = true;
        Assert.Null(draft.ToRequest());
        Assert.True(draft.ClearsPlan);
    }

    [Fact]
    public void A_linked_contract_is_shown_but_not_taken_as_an_own_plan()
    {
        var linked = WithPlan() with { Plan = WithPlan().Plan! with { IsOwnPlan = false } };

        var draft = DebtPlanDraft.From(linked, Today);

        Assert.False(draft.HasOwnPlan);
        Assert.False(draft.Enabled);
    }
}
