using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;

namespace Duofy.Shared.Tests;

/// <summary>"Jetzt verfügbar" (Spec 139) neben "Verplanbar im Monat": eine Komponente für alle Orte. "Heute" ist der 21. Oktober 2026.</summary>
public class AvailableNowPageTests : PageTestContext
{
    private static PlanOverviewResponse Overview(AvailableNowResponse? availableNow) =>
        new(3000m, 1200m, 1800m, [new PlanPotResponse(PotKeys.Needs, 1000m, 0m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)], [])
        { AvailableNow = availableNow };

    private IRenderedComponent<Plan> OpenMonth(AvailableNowResponse? availableNow)
    {
        RenderDialogHost();
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = Overview(availableNow) };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-metrics']")));
        return cut;
    }

    // A4 (R3): beide Zahlen und die Aufschlüsselung im laufenden Monat.
    [Fact]
    public void The_month_shows_available_now_with_its_breakdown_next_to_allocatable()
    {
        var cut = OpenMonth(new AvailableNowResponse(1050m, 860m, 190m));

        var block = cut.Find("[data-testid='month-block-available-now']").TextContent;
        Assert.Contains("Jetzt verfügbar", block);
        Assert.Contains("190,00 €", block);
        Assert.Contains("Konten 1.050,00 € − offen bis Monatsende 860,00 €", block);
        Assert.Contains("1.800,00 €", cut.Find("[data-testid='month-block-free']").TextContent);
    }

    // A3 (R2): neues Wort und Hinweis, Zahl unverändert.
    [Fact]
    public void Allocatable_is_labelled_per_month_with_the_hint_about_planned_income()
    {
        var cut = OpenMonth(null);

        var free = cut.Find("[data-testid='month-block-free']").TextContent;
        Assert.Contains("Verplanbar im Monat", free);
        Assert.Contains("inklusive geplanter Einnahmen dieses Monats", free);
        Assert.Contains("1.800,00 €", free);
    }

    // A4: in einem anderen Monat nur Verplanbar im Monat.
    [Fact]
    public void Without_a_value_there_is_no_available_now_block()
    {
        var cut = OpenMonth(null);

        Assert.Empty(cut.FindAll("[data-testid='month-block-available-now']"));
    }

    [Fact]
    public void A_negative_value_is_shown_as_negative()
    {
        var cut = OpenMonth(new AvailableNowResponse(50m, 200m, -150m));

        Assert.Contains("-150,00 €", cut.Find("[data-testid='month-block-available-now']").TextContent.Replace("−", "-"));
    }

    // A5 (R4): wer nicht eingerechnet ist, steht im Hinweis.
    [Fact]
    public void A_household_value_names_the_members_that_are_not_included()
    {
        var cut = Render<AvailableNowMetric>(p => p.Add(x => x.Value, new AvailableNowResponse(1000m, 400m, 600m, ["Jasmin"])));

        Assert.Contains("Nicht eingerechnet: Jasmin.", cut.Markup);
    }
}
