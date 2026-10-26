using Duofy.Domain;

namespace Duofy.Domain.Tests;

public class CommitmentDueTests
{
    // Alle Beispielverträge gehören demselben ausgedachten Benutzer.
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Monthly_commitment_is_due_in_its_first_month()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 10, 1));

        Assert.True(rent.IsDueIn(2026, 10));
    }

    [Fact]
    public void Monthly_commitment_is_due_in_every_later_month()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 10, 1));

        Assert.True(rent.IsDueIn(2026, 11));
        Assert.True(rent.IsDueIn(2027, 3));
    }

    [Fact]
    public void Commitment_is_not_due_before_its_first_due_date()
    {
        var rent = Every(months: 1, firstDue: new DateOnly(2026, 10, 1));

        Assert.False(rent.IsDueIn(2026, 9));
    }

    [Fact]
    public void Quarterly_commitment_is_due_every_third_month_only()
    {
        var insurance = Every(months: 3, firstDue: new DateOnly(2026, 2, 15));

        Assert.True(insurance.IsDueIn(2026, 5));
        Assert.False(insurance.IsDueIn(2026, 3));
        Assert.False(insurance.IsDueIn(2026, 4));
    }

    [Fact]
    public void Interval_keeps_counting_across_the_turn_of_the_year()
    {
        var subscription = Every(months: 5, firstDue: new DateOnly(2026, 11, 1));

        Assert.True(subscription.IsDueIn(2027, 4));
        Assert.True(subscription.IsDueIn(2027, 9));
        Assert.False(subscription.IsDueIn(2027, 1));
    }

    [Fact]
    public void Commitment_is_still_due_in_the_month_it_ends()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 1), lastDue: new DateOnly(2026, 6, 10));

        Assert.True(gym.IsDueIn(2026, 6));
    }

    [Fact]
    public void Commitment_is_not_due_after_the_month_it_ends()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 1), lastDue: new DateOnly(2026, 6, 10));

        Assert.False(gym.IsDueIn(2026, 7));
        Assert.False(gym.IsDueIn(2027, 1));
    }

    [Fact]
    public void Commitment_is_not_due_in_its_last_month_when_the_due_day_comes_after_the_end()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 15), lastDue: new DateOnly(2026, 6, 10));

        // Im Mai kommt die Abbuchung am 15. noch, im Juni endet der Vertrag schon am 10.
        Assert.True(gym.IsDueIn(2026, 5));
        Assert.False(gym.IsDueIn(2026, 6));
    }

    [Fact]
    public void Commitment_is_due_when_the_due_day_is_the_day_it_ends()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 10), lastDue: new DateOnly(2026, 6, 10));

        Assert.True(gym.IsDueIn(2026, 6));
    }

    [Fact]
    public void Shortened_due_day_counts_when_checking_against_the_end()
    {
        var gym = Every(months: 1, firstDue: new DateOnly(2026, 1, 31), lastDue: new DateOnly(2026, 2, 28));

        // Zahltag 31 wird im Februar zum 28., und der liegt nicht nach dem Ende.
        Assert.True(gym.IsDueIn(2026, 2));
    }

    // Baut einen Vertrag, bei dem sich nur die Felder ändern, die über den Fälligkeitsmonat entscheiden.
    private static Commitment Every(int months, DateOnly firstDue, DateOnly? lastDue = null)
    {
        return new Commitment
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
}
