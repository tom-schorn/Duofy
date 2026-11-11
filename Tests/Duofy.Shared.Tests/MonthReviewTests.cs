using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

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
    public void The_month_offers_the_tab_review_after_bookings()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = Data() };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='month-tab-review']")));

        Assert.Equal(["Plan", "Buchungen", "Auswertung"], cut.FindAll("[role='tab']").Select(t => t.TextContent.Trim()));
    }

    // Spec 82, R14 (A16): ein rundes Diagramm geplant gegen gebucht für die drei Töpfe, außen geplant, innen gebucht.
    [Fact]
    public void The_review_shows_a_round_chart_of_the_pots_planned_against_booked()
    {
        var cut = OpenReview(2026, 10, Data());

        var pots = cut.Find("[data-testid='month-review-pots-chart']");
        Assert.Equal(3, pots.QuerySelectorAll("circle[data-ring='planned']").Length);
        Assert.Equal(3, pots.QuerySelectorAll("circle[data-ring='booked']").Length);
        Assert.Equal(["needs", "wants", "savings"],
            pots.QuerySelectorAll("circle[data-ring='planned']").Select(c => c.ClassList.First(k => k.StartsWith("donut-tone--"))["donut-tone--".Length..]));
        var legend = pots.QuerySelector(".donut-legend")!.TextContent;
        Assert.Contains("1.200,00 €", legend);
        Assert.Contains("300,00 €", legend);
        Assert.Contains("600,00 €", legend);
        Assert.Contains("Gebucht 2.000,00 € von geplant 1.800,00 €.", pots.TextContent);
        Assert.Contains("111 %", pots.QuerySelector(".donut-center-value")!.TextContent);
    }

    // Spec 82, R14 (A16): das runde Diagramm der Kategorien, die größten einzeln und der Rest als "Weitere".
    [Fact]
    public void The_review_shows_a_round_chart_of_the_categories_with_the_rest_as_others()
    {
        var categories = Enumerable.Range(1, 7)
            .Select(i => FakePlanApi.Position($"Posten {i}", 100m * i, booked: 10m * i) with { CategoryId = Guid.NewGuid() })
            .ToArray();
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Overview = Data(), Positions = categories };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenReviewTab(cut);

        var chart = cut.Find("[data-testid='month-review-categories-chart']");
        var names = chart.QuerySelectorAll(".donut-legend tbody th").Select(th => th.TextContent.Trim()).ToList();
        Assert.Equal(6, names.Count);
        Assert.Equal("Weitere", names[^1]);
        Assert.Contains("300,00 €", chart.QuerySelector(".donut-legend tbody tr:last-child")!.TextContent);
    }

    // Spec 82, R14 (A16): Die Tageskurve zeigt mit einer offenen Ausgabe, die den Stand unter null drückt, Tag und Betrag der Lücke.
    [Fact]
    public void A16_the_day_curve_marks_where_an_open_expense_pushes_the_balance_below_zero()
    {
        var salary = FakePlanApi.Position("Gehalt", 1500m, PotKeys.Income, booked: 1500m, done: true, day: 1);
        var rent = FakePlanApi.Position("Miete", 1200m, day: 25);
        var car = FakePlanApi.Position("Auto", 600m, day: 28);
        var paid = new BookingResponse(Guid.NewGuid(), BookingKinds.Income, Guid.NewGuid(), null, new DateOnly(2026, 10, 1), 1500m, null, [], 2026, 10);
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [salary, rent, car],
            Overview = Data() with { Bookings = [paid] },
        };
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 10));
        OpenReviewTab(cut);

        // A16 seit 03.11.2026: kein Sankey.
        Assert.Empty(cut.FindAll("[data-testid='month-flow']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='month-review-pots-chart']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='month-review-categories-chart']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='month-curve-marker']"));
        var text = Text(cut, "month-curve-text");
        Assert.Contains("Mittwoch, 28. Oktober", text);
        Assert.Contains("300,00 €", text);
        // Tom, 03.11.2026: Beträge an der Achse (null, Start, Tiefpunkt), die Null-Linie und die Lücke als Fläche.
        var axis = cut.FindAll("[data-testid='month-curve-axis']").Select(t => t.TextContent.Trim()).ToList();
        Assert.Contains("0 €", axis);
        Assert.Contains("1.500 €", axis);
        Assert.Contains("-300 €", axis);
        Assert.NotNull(cut.Find("[data-testid='month-curve-zero']"));
        Assert.NotNull(cut.Find("[data-testid='month-curve-gap-area']").GetAttribute("clip-path"));
        // Am Handy ein eigenes, höheres Bild mit eigener Schriftgröße, damit die Achse lesbar bleibt.
        // Tom, 03.11.2026: kleiner, am PC etwa 200 und am Handy etwa 180 Pixel bei voller Kartenbreite.
        Assert.Equal("0 0 640 160", cut.Find("svg.day-curve-wide").GetAttribute("viewBox"));
        Assert.Equal("0 0 360 200", cut.Find("svg.day-curve-narrow").GetAttribute("viewBox"));
    }

    // Textalternative: Jedes Diagramm hat einen Satz und eine Tabelle für Vorlese-Programme; der gemalte Teil ist verborgen.
    [Fact]
    public void Every_chart_has_a_text_alternative()
    {
        var cut = OpenReview(2026, 10, Data());

        Assert.Contains("Insgesamt gebucht: 2.000,00 €, geplant: 1.800,00 €.", Text(cut, "month-review-total"));

        // Spec 82, R14: Die runden Diagramme tragen ihre Zahlen in einer Tabelle mit Überschrift; der gemalte Teil ist verborgen.
        var pots = cut.Find("[data-testid='month-review-pots-chart']");
        Assert.Equal("true", pots.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.NotNull(pots.QuerySelector("table.donut-legend caption"));
        Assert.NotNull(pots.QuerySelector("figcaption"));

        // Die Tageskurve hat einen Satz und eine Tabelle aller Tage.
        var curve = cut.Find("[data-testid='month-curve']");
        Assert.Equal("true", curve.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Equal(31, curve.QuerySelectorAll(".visually-hidden tbody tr").Length);
        Assert.NotEmpty(Text(cut, "month-curve-text"));
        // Ohne Lücke gibt es keine Lückenfläche, wohl aber die Achse mit null.
        Assert.Empty(cut.FindAll("[data-testid='month-curve-gap-area']"));
        Assert.Contains("0 €", cut.FindAll("[data-testid='month-curve-axis']").Select(t => t.TextContent.Trim()));
    }

    // Auch ein abgeschlossener Monat (vor dem heutigen) hat seine Auswertung, mit seinen eigenen Zahlen.
    [Fact]
    public void A_past_month_has_its_own_review()
    {
        var cut = OpenReview(2026, 9, Data() with { Pots = [new PlanPotResponse(PotKeys.Needs, 400m, 450m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)] });

        Assert.Contains("September 2026", Text(cut, "page-title"));
        Assert.Contains("Insgesamt gebucht: 450,00 €, geplant: 400,00 €.", Text(cut, "month-review-total"));
        Assert.Empty(cut.FindAll("[data-testid='month-review-empty']"));
    }

    // Der Tab bleibt beim Blättern zwischen den Monaten gewählt, wie die anderen Tabs.
    [Fact]
    public void The_review_stays_chosen_while_paging_to_another_month()
    {
        PlanApiFake.Plans[(2026, 9)] = FakePlanApi.Empty(2026, 9) with { Overview = NoData() };
        var cut = OpenReview(2026, 10, Data());

        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));

        cut.WaitForAssertion(() => Assert.Contains("September 2026", Text(cut, "page-title")));
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
        // Spec 125: Die Zentrale hat denselben Knopf wie der eigene Plan.
        Assert.NotEmpty(cut.FindAll("[data-testid='month-action-booking']"));
    }
}
