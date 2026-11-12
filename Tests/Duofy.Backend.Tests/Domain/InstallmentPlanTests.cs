using Duofy.Schemas;

namespace Duofy.Domain.Tests;

/// <summary>Die Raten-Rechnung einer Schuld (Issue 137): in Cent, mit kleinerer letzter Rate.</summary>
public class InstallmentPlanTests
{
    private static readonly DateOnly First = new(2026, 12, 1);

    [Fact]
    public void The_real_example_gives_seven_times_fifty_and_a_last_installment_of_36_94_on_1_july_2027()
    {
        var open = 786.94m - 400m;

        var plan = InstallmentPlan.Compute(open, 50m, 1, First, null);

        Assert.Equal(InstallmentPlanError.None, plan.Error);
        Assert.Equal(8, plan.Count);
        Assert.Equal(36.94m, plan.LastAmount);
        Assert.Equal(new DateOnly(2027, 7, 1), plan.LastDueDate);
    }

    [Fact]
    public void An_exact_division_has_no_smaller_last_installment()
    {
        var plan = InstallmentPlan.Compute(200m, 50m, 1, First, null);

        Assert.Equal(4, plan.Count);
        Assert.Equal(50m, plan.LastAmount);
        Assert.Equal(new DateOnly(2027, 3, 1), plan.LastDueDate);
    }

    [Fact]
    public void One_cent_over_a_full_division_needs_one_more_installment_of_one_cent()
    {
        var plan = InstallmentPlan.Compute(200.01m, 50m, 1, First, null);

        Assert.Equal(5, plan.Count);
        Assert.Equal(0.01m, plan.LastAmount);
    }

    [Fact]
    public void The_rhythm_spreads_the_due_dates()
    {
        var plan = InstallmentPlan.Compute(300m, 100m, 3, First, null);

        Assert.Equal(3, plan.Count);
        Assert.Equal(new DateOnly(2027, 6, 1), plan.LastDueDate);
    }

    [Fact]
    public void An_open_amount_below_the_rate_is_one_installment_of_the_open_amount()
    {
        var plan = InstallmentPlan.Compute(20m, 50m, 1, First, null);

        Assert.Equal(1, plan.Count);
        Assert.Equal(20m, plan.LastAmount);
        Assert.Equal(First, plan.LastDueDate);
    }

    [Fact]
    public void A_last_installment_set_by_hand_is_used_and_the_count_follows_from_it()
    {
        // 386,94 - 86,94 = 300 -> 6 full installments, then the hand-set last one: 7 in all, exactly the open amount.
        var plan = InstallmentPlan.Compute(386.94m, 50m, 1, First, 86.94m);

        Assert.Equal(7, plan.Count);
        Assert.Equal(86.94m, plan.LastAmount);
        Assert.Equal(new DateOnly(2027, 6, 1), plan.LastDueDate);
    }

    [Fact]
    public void A_hand_set_last_installment_is_cut_so_the_total_never_exceeds_the_open_amount()
    {
        // 386,94 - 40 = 346,94 -> 7 full installments (350); 350 + 40 would be 390, so the last one is cut to 36,94.
        var plan = InstallmentPlan.Compute(386.94m, 50m, 1, First, 40m);

        Assert.Equal(8, plan.Count);
        Assert.Equal(36.94m, plan.LastAmount);
        Assert.Equal(386.94m, 7 * 50m + plan.LastAmount);
    }

    [Theory]
    [InlineData(450, 100, 36.94)] // five full rates already pay more than 450 - 36,94: nothing is left for the last one
    [InlineData(100, 50, 100)]   // as large as the open amount
    [InlineData(100, 50, 150)]   // larger than the open amount
    public void A_hand_set_last_installment_that_does_not_fit_is_refused(int open, int rate, double last)
    {
        var plan = InstallmentPlan.Compute(open, rate, 1, First, (decimal)last);

        Assert.Equal(InstallmentPlanError.LastAmountDoesNotFit, plan.Error);
    }

    [Fact]
    public void More_than_600_installments_are_refused()
    {
        var plan = InstallmentPlan.Compute(30_001m, 50m, 1, First, null);

        Assert.Equal(InstallmentPlanError.TooManyInstallments, plan.Error);
    }

    [Fact]
    public void Exactly_600_installments_are_allowed()
    {
        var plan = InstallmentPlan.Compute(30_000m, 50m, 1, First, null);

        Assert.Equal(InstallmentPlanError.None, plan.Error);
        Assert.Equal(600, plan.Count);
    }

    [Fact]
    public void Nothing_open_or_a_rate_of_zero_is_refused()
    {
        Assert.Equal(InstallmentPlanError.NothingOpen, InstallmentPlan.Compute(0m, 50m, 1, First, null).Error);
        Assert.Equal(InstallmentPlanError.RateInvalid, InstallmentPlan.Compute(100m, 0m, 1, First, null).Error);
    }

    [Fact]
    public void The_amount_in_a_month_is_the_last_amount_only_in_the_last_due_month()
    {
        var contract = new Commitment
        {
            Name = "Rate", Amount = 50m, Type = CommitmentType.Debt, OwnerId = Guid.NewGuid(), IntervalMonths = 1,
            FirstDueDate = First, LastDueDate = new DateOnly(2027, 7, 1), LastAmount = 36.94m,
        };

        Assert.Equal(50m, contract.AmountIn(2027, 6));
        Assert.Equal(36.94m, contract.AmountIn(2027, 7));
    }
}
