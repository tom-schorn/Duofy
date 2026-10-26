using Duofy.Domain;

namespace Duofy.Domain.Tests;

/// <summary>
/// Die Tage, an denen ein Vertrag zuletzt gezahlt hat und als Nächstes zahlt (Spec 64, R8 und R10).
/// Der Knopf "Beenden" braucht die letzte Zahlung bis heute, die Liste die nächste ab heute.
/// </summary>
public class CommitmentPaymentDatesTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // A10: monatlich ab 1. Januar, heute 23. Oktober: zuletzt am 1. Oktober.
    [Fact]
    public void Last_payment_of_a_monthly_contract_is_the_first_of_this_month()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), rent.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void A_payment_on_the_day_itself_counts_as_the_last_one()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 1, 23));

        Assert.Equal(new DateOnly(2026, 10, 23), rent.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void Before_this_months_due_day_the_last_payment_was_last_month()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 1, 28));

        Assert.Equal(new DateOnly(2026, 9, 28), rent.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_last_payment_follows_the_interval()
    {
        // Vierteljährlich ab Februar: Februar, Mai, August, November.
        var insurance = Every(months: 3, firstDue: new DateOnly(2026, 2, 15));

        Assert.Equal(new DateOnly(2026, 8, 15), insurance.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_last_payment_uses_the_last_day_of_a_short_month()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 31));

        Assert.Equal(new DateOnly(2026, 2, 28), gym.LastPaymentOnOrBefore(new DateOnly(2026, 3, 30)));
    }

    // A11: Vor der ersten Zahlung gibt es keine letzte.
    [Fact]
    public void Before_the_first_payment_there_is_no_last_payment()
    {
        var contract = Every(months: 1, firstDue: new DateOnly(2026, 12, 15));

        Assert.Null(contract.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void An_ended_contract_keeps_its_last_payment()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 1), lastDue: new DateOnly(2026, 5, 1));

        Assert.Equal(new DateOnly(2026, 5, 1), gym.LastPaymentOnOrBefore(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_next_payment_is_this_months_due_day_when_it_is_still_ahead()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 1, 28));

        Assert.Equal(new DateOnly(2026, 10, 28), rent.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_next_payment_is_next_month_once_this_months_has_passed()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), rent.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_next_payment_of_a_future_contract_is_its_first_payment()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 11, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), rent.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void The_next_payment_follows_the_interval_across_the_year_end()
    {
        var insurance = Every(months: 12, firstDue: new DateOnly(2025, 3, 1));

        Assert.Equal(new DateOnly(2027, 3, 1), insurance.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void An_ended_contract_has_no_next_payment()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 1), lastDue: new DateOnly(2026, 10, 1));

        Assert.Null(gym.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void A_contract_ending_later_still_has_its_next_payment()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 1), lastDue: new DateOnly(2026, 11, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), gym.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void A_contract_that_ends_before_its_first_payment_never_pays()
    {
        var never = Every(months: 1, firstDue: new DateOnly(2026, 12, 15), lastDue: new DateOnly(2026, 12, 14));

        Assert.Null(never.NextPaymentOnOrAfter(new DateOnly(2026, 10, 23)));
        Assert.Null(never.LastPaymentOnOrBefore(new DateOnly(2027, 6, 1)));
    }

    [Fact]
    public void Savings_goals_and_debts_always_use_the_pot_savings_and_income_the_pot_income()
    {
        Assert.Equal(Pot.Savings, Commitment.FixedPotFor(CommitmentType.SavingsGoal));
        Assert.Equal(Pot.Savings, Commitment.FixedPotFor(CommitmentType.Debt));
        Assert.Equal(Pot.Income, Commitment.FixedPotFor(CommitmentType.Income));
        Assert.Null(Commitment.FixedPotFor(CommitmentType.Contract));
    }

    private static Commitment Every(int months, DateOnly firstDue, DateOnly? lastDue = null) => new()
    {
        Name = "Example",
        Amount = 100.00m,
        Type = CommitmentType.Contract,
        OwnerId = Owner,
        IntervalMonths = months,
        FirstDueDate = firstDue,
        LastDueDate = lastDue,
    };
}
