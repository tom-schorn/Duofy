using Duofy.Shared.Services;

namespace Duofy.Shared.Tests;

/// <summary>Die Regel der Plan-Übersicht (Spec 23, R2 bis R4) ohne Oberfläche.</summary>
public class PlanOverviewRowsTests
{
    private static readonly DateOnly October21 = new(2026, 10, 21);

    [Fact]
    public void Plans_plus_the_current_and_three_coming_months_make_the_rows_newest_first()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 100m), (2026, 9, 50m)], October21);

        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10", "2026-9"], rows.Select(r => r.Key));
    }

    [Fact]
    public void Only_the_current_month_is_current_and_only_months_with_a_plan_have_a_free_amount()
    {
        var rows = PlanOverviewRows.Build([(2026, 10, 100m), (2026, 9, -50m)], October21);

        Assert.Equal(["2026-10"], rows.Where(r => r.IsCurrent).Select(r => r.Key));
        Assert.Equal([null, null, null, 100m, -50m], rows.Select(r => r.Free));
    }

    [Fact]
    public void Without_any_plan_the_current_month_and_the_coming_ones_remain()
    {
        var rows = PlanOverviewRows.Build([], October21);

        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10"], rows.Select(r => r.Key));
    }

    [Fact]
    public void A_row_links_to_the_month_plan()
    {
        var row = Assert.Single(PlanOverviewRows.Build([(2026, 9, 1m)], new DateOnly(2026, 9, 30)), r => r.Month == 9);

        Assert.Equal("/plan/2026/9", row.Href);
    }
}
