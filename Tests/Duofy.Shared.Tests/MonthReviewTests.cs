using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Duofy.Shared.Tests;

/// <summary>
/// Issue #26 (Monatsende-Auswertung): Der vierte Tab "Auswertung" im Monat zeigt mit den vorhandenen Diagrammen, wohin
/// das Geld ging (Verteilung) und wie Geplantes und Gebuchtes zueinander stehen (Soll gegen Ist). Jedes Diagramm hat eine
/// Textalternative, ein Monat ohne Daten einen Leerzustand, und der Tab gilt für jeden Monat, auch für vergangene.
/// "Heute" ist der 21. Oktober 2026 (feste Uhr der Seitentests). Fremde Beträge nur so, wie sie der Haushaltsplan zeigt.
/// Der echte Browser ist damit nicht gesehen.
/// </summary>
public class MonthReviewTests : PageTestContext
{
    public MonthReviewTests()
    {
        Render<MudPopoverProvider>();
        Render<MudDialogProvider>();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    // Grundbedarf 1.000 geplant, 1.200 gebucht; Wünsche 300 und 200; Sparen 500 und 600: gebucht 2.000, geplant 1.800.
    private static PlanOverviewResponse Data() =>
        new(3000m, 1800m, 1200m,
            [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m), new PlanPotResponse(PotKeys.Wants, 300m, 200m), new PlanPotResponse(PotKeys.Savings, 500m, 600m)],
            []);

    private static PlanOverviewResponse NoData() =>
        new(0m, 0m, 0m,
            [new PlanPotResponse(PotKeys.Needs, 0m, 0m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)],
            []);

    private IRenderedComponent<Plan> OpenReview(int year, int month, PlanOverviewResponse overview)
    {
        PlanApiFake.Plans[(year, month)] = FakePlanApi.Empty(year, month) with { Overview = overview };
        var cut = Render<Plan>(p => p.Add(x => x.Year, year).Add(x => x.Month, month));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));
        cut.Find("[data-testid='month-tab-review']").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review']")));
        return cut;
    }

    private static string Text(IRenderedComponent<Plan> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    // Der Tab ist aus dem Monat erreichbar: vierter Tab nach den dreien.
    [Fact]
    public void The_month_offers_a_fourth_tab_review_after_bookings()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = Data() };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));

        Assert.Equal(["Übersicht", "Plan", "Buchungen", "Auswertung"], cut.FindAll("[role='tab']").Select(t => t.TextContent.Trim()));
    }

    // Mindestens zwei Diagramme: die Verteilung der gebuchten Beträge und Soll gegen Ist.
    [Fact]
    public void The_review_shows_the_distribution_and_planned_against_booked_as_charts()
    {
        var cut = OpenReview(2026, 10, Data());

        var distribution = cut.Find("[data-testid='month-review-distribution']");
        Assert.NotNull(distribution.QuerySelector("figure.chart .chart-bars"));
        Assert.Equal(3, distribution.QuerySelectorAll(".chart-bar-row").Length);
        Assert.Contains("1.200,00 €", distribution.TextContent);

        var versus = cut.Find("[data-testid='month-review-plan-vs-actual']");
        Assert.Equal(3, versus.QuerySelectorAll("figure.chart").Length);
        Assert.Contains("Ist 1.200,00 von Soll 1.000,00", versus.TextContent);
        Assert.Contains("Ist 200,00 von Soll 300,00", versus.TextContent);
        Assert.Contains("Ist 600,00 von Soll 500,00", versus.TextContent);
    }

    // Textalternative: Jedes Diagramm hat einen Satz und eine Tabelle für Vorlese-Programme; der gemalte Teil ist verborgen.
    [Fact]
    public void Every_chart_has_a_text_alternative()
    {
        var cut = OpenReview(2026, 10, Data());

        Assert.Contains("Insgesamt gebucht: 2.000,00 €, geplant: 1.800,00 €.", Text(cut, "month-review-total"));
        Assert.Contains("Den größten Anteil hat Grundbedarf mit 60 Prozent.", Text(cut, "month-review-distribution-text"));

        var distribution = cut.Find("[data-testid='month-review-distribution']");
        Assert.NotNull(distribution.QuerySelector(".visually-hidden table"));
        Assert.Equal("true", distribution.QuerySelector(".chart-bars")!.GetAttribute("aria-hidden"));

        var versus = cut.Find("[data-testid='month-review-plan-vs-actual']");
        Assert.Equal(3, versus.QuerySelectorAll(".visually-hidden table").Length);
        Assert.Equal(3, versus.QuerySelectorAll("figcaption").Length);
    }

    // Auch ein abgeschlossener Monat (vor dem heutigen) hat seine Auswertung, mit seinen eigenen Zahlen.
    [Fact]
    public void A_past_month_has_its_own_review()
    {
        var cut = OpenReview(2026, 9, Data() with { Pots = [new PlanPotResponse(PotKeys.Needs, 400m, 450m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)] });

        Assert.Contains("September 2026", Text(cut, "plan-month"));
        Assert.Contains("Insgesamt gebucht: 450,00 €, geplant: 400,00 €.", Text(cut, "month-review-total"));
        Assert.Empty(cut.FindAll("[data-testid='month-review-empty']"));
    }

    // Der Tab bleibt beim Blättern zwischen den Monaten gewählt, wie die anderen Tabs.
    [Fact]
    public void The_review_stays_chosen_while_paging_to_another_month()
    {
        PlanApiFake.Plans[(2026, 9)] = FakePlanApi.Empty(2026, 9) with { Overview = NoData() };
        var cut = OpenReview(2026, 10, Data());

        cut.Find("[data-testid='plan-previous']").Click();

        cut.WaitForAssertion(() => Assert.Contains("September 2026", Text(cut, "plan-month")));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review']")));
    }

    // Leerzustand: Ohne Geplantes und Gebuchtes steht ein Satz da, kein leeres Diagramm.
    [Fact]
    public void A_month_without_data_shows_the_empty_state_and_no_chart()
    {
        var cut = OpenReview(2026, 8, NoData());

        Assert.Contains("nichts auszuwerten", Text(cut, "month-review-empty"));
        Assert.Empty(cut.FindAll("[data-testid='month-review'] figure.chart"));
        Assert.Empty(cut.FindAll("[data-testid='month-review-total']"));
    }

    // Ein Monat, den die API ohne Überblick liefert, ist ebenfalls leer und stürzt nicht ab.
    [Fact]
    public void A_month_without_an_overview_is_empty_too()
    {
        PlanApiFake.Plans[(2026, 7)] = FakePlanApi.Empty(2026, 7) with { Overview = null };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 7));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));

        cut.Find("[data-testid='month-tab-review']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review-empty']")));
    }

    // Spec 68: Ohne Sicht auf die Buchungen ist jeder gebuchte Betrag 0; statt falscher Diagramme steht, dass nichts geteilt wird.
    [Fact]
    public async Task Without_sight_of_bookings_the_review_says_not_shared_instead_of_charts()
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = new PlanOverviewResponse(0m, 890m, -890m, [new PlanPotResponse(PotKeys.Needs, 890m, 0m)], []) };
        var cut = Render<Plan>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));

        cut.Find("[data-testid='month-tab-review']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review-not-shared']")));
        Assert.Contains("teilt diesen Bereich nicht", Text(cut, "month-review-not-shared"));
        Assert.Empty(cut.FindAll("[data-testid='month-review'] figure.chart"));
    }

    // Haushalt: nur lesend, die Summen kommen aus dem Haushaltsplan, und wer nichts freigibt, steht dabei wie in der Übersicht.
    [Fact]
    public async Task The_household_review_uses_the_household_plan_and_names_those_who_share_nothing()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: true), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Income = 3000m,
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 1000m, 1200m), new HouseholdPotResponse(PotKeys.Wants, 300m, 200m), new HouseholdPotResponse(PotKeys.Savings, 500m, 600m)],
            BookingsMissing = ["Ben"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));

        cut.Find("[data-testid='month-tab-review']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-review']")));
        Assert.Contains("Insgesamt gebucht: 2.000,00 €, geplant: 1.800,00 €.", Text(cut, "month-review-total"));
        Assert.Contains("Ben", Text(cut, "month-review-missing"));
        Assert.Empty(cut.FindAll("[data-testid='month-action-booking']"));
    }
}
