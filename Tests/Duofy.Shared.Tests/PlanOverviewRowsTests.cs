using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Die Regel der Plan-Übersicht (Spec 23, Issue #94, R6) ohne Oberfläche.</summary>
public class PlanOverviewRowsTests
{
    private static readonly DateOnly October21 = new(2026, 10, 21);

    [Fact]
    public void The_current_month_comes_first_then_the_past_months_newest_first()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 100m, false), (2026, 9, 50m, false), (2026, 8, 5m, false)], October21);

        Assert.Equal(["2026-10", "2026-9", "2026-8"], rows.Select(r => r.Key));
    }

    // A6: keine leeren Karten für künftige Monate
    [Fact]
    public void Future_months_appear_only_when_they_have_a_plan()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 100m, false), (2026, 9, 50m, false)], October21);

        Assert.DoesNotContain(rows, r => r.Year > 2026 || r.Month > 10);
    }

    [Fact]
    public void An_existing_future_month_follows_the_current_one_nearest_first()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 1m, false), (2027, 1, 3m, false), (2026, 11, 2m, false), (2026, 9, 4m, false)], October21);

        Assert.Equal(["2026-10", "2026-11", "2027-1", "2026-9"], rows.Select(r => r.Key));
    }

    [Fact]
    public void Only_the_current_month_is_current_and_only_months_with_a_plan_have_a_free_amount()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 100m, true), (2026, 9, -50m, false)], October21);

        Assert.Equal(["2026-10"], rows.Where(r => r.IsCurrent).Select(r => r.Key));
        Assert.Equal([100m, -50m], rows.Select(r => r.Free));
        Assert.Equal(["2026-10"], rows.Where(r => r.WithCarryOver).Select(r => r.Key));
    }

    [Fact]
    public void Without_any_plan_only_the_current_month_remains()
    {
        var rows = PlanOverviewRows.Build([], October21);

        var row = Assert.Single(rows);
        Assert.Equal(("2026-10", null), (row.Key, row.Free));
    }

    [Fact]
    public void A_row_links_to_the_month_plan()
    {
        var row = Assert.Single(PlanOverviewRows.Build([(2026, 9, 1m, false)], new DateOnly(2026, 9, 30)), r => r.Month == 9);

        Assert.Equal("/plan/2026/9", row.Href);
    }

    [Fact]
    public void The_next_month_to_create_follows_the_current_one_when_nothing_lies_ahead()
    {
        Assert.Equal((2026, 11), PlanOverviewRows.NextToCreate([(2026, 10, 1m, false), (2026, 9, 1m, false)], October21));
        Assert.Equal((2027, 1), PlanOverviewRows.NextToCreate([], new DateOnly(2026, 12, 3)));
    }

    [Fact]
    public void The_next_month_to_create_follows_the_last_existing_future_month()
    {
        Assert.Equal((2027, 1), PlanOverviewRows.NextToCreate([(2026, 10, 1m, false), (2026, 12, 1m, false)], October21));
    }
}
