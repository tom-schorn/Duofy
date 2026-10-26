using Duofy.Domain;

namespace Duofy.Domain.Tests;

public class CommitmentRunsTests
{
    // Alle Beispielverträge gehören demselben ausgedachten Benutzer.
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Commitment_without_end_always_runs()
    {
        var rent = EndingOn(null);

        Assert.True(rent.RunsOn(new DateOnly(2026, 5, 20)));
        Assert.True(rent.RunsOn(new DateOnly(2040, 1, 1)));
    }

    [Fact]
    public void Commitment_runs_on_a_day_before_its_end()
    {
        var gym = EndingOn(new DateOnly(2026, 6, 10));

        Assert.True(gym.RunsOn(new DateOnly(2026, 5, 20)));
        Assert.True(gym.RunsOn(new DateOnly(2026, 6, 9)));
    }

    [Fact]
    public void Commitment_still_runs_on_the_day_it_ends()
    {
        var gym = EndingOn(new DateOnly(2026, 6, 10));

        Assert.True(gym.RunsOn(new DateOnly(2026, 6, 10)));
    }

    [Fact]
    public void Commitment_does_not_run_on_the_day_after_its_end()
    {
        var gym = EndingOn(new DateOnly(2026, 6, 10));

        // Derselbe Monat, aber das Ende ist vorbei.
        Assert.False(gym.RunsOn(new DateOnly(2026, 6, 11)));
        Assert.False(gym.RunsOn(new DateOnly(2026, 6, 25)));
    }

    [Fact]
    public void Commitment_does_not_run_in_the_month_after_its_end()
    {
        var gym = EndingOn(new DateOnly(2026, 6, 10));

        Assert.False(gym.RunsOn(new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void End_in_december_does_not_reach_into_january()
    {
        var gym = EndingOn(new DateOnly(2026, 12, 31));

        Assert.False(gym.RunsOn(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Earlier_month_number_in_a_later_year_is_still_after_the_end()
    {
        var gym = EndingOn(new DateOnly(2026, 6, 10));

        // Februar ist kleiner als Juni, liegt hier aber ein Jahr später.
        Assert.False(gym.RunsOn(new DateOnly(2027, 2, 1)));
    }

    // Baut einen Vertrag, bei dem sich nur die letzte Zahlung ändert.
    private static Commitment EndingOn(DateOnly? lastDue)
    {
        return new Commitment
        {
            Name = "Example",
            Amount = 100.00m,
            Type = CommitmentType.Contract,
            OwnerId = Owner,
            IntervalMonths = 1,
            FirstDueDate = new DateOnly(2026, 1, 1),
            LastDueDate = lastDue,
        };
    }
}
