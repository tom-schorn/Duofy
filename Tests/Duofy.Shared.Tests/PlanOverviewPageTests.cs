using System.Reflection;
using Bunit;
using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Plan-Übersicht (Spec 23, A1 bis A6): /plan zeigt zuerst die Monatsliste mit dem laufenden Monat hervorgehoben
/// und dem Frei-Betrag je Monat; ein Tipp öffnet /plan/{Jahr}/{Monat}, und der Monatsplan führt zurück. "Heute" ist
/// der 21. Oktober 2026.
/// </summary>
public class PlanOverviewPageTests : PageTestContext
{
    public PlanOverviewPageTests()
    {
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private IRenderedComponent<PlanOverview> OpenOverview()
    {
        var cut = Render<PlanOverview>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-months']")));
        return cut;
    }

    private static IReadOnlyList<string> RowIds(IRenderedComponent<PlanOverview> cut) =>
        cut.FindAll("[data-testid^='plan-month-row-']").Select(r => r.GetAttribute("data-testid")!["plan-month-row-".Length..]).ToList();

    private static string Row(IRenderedComponent<PlanOverview> cut, string id) => cut.Find($"[data-testid='plan-month-row-{id}']").TextContent;

    // A1
    [Fact]
    public void The_overview_is_the_page_at_plan_and_needs_a_sign_in()
    {
        Assert.Equal("/plan", typeof(PlanOverview).GetCustomAttributes<RouteAttribute>().Single().Template);
        Assert.NotNull(typeof(PlanOverview).GetCustomAttribute<AuthorizeAttribute>());
    }

    private static PlanOverviewMonthResponse Month(int year, int month, decimal free, bool carriedIn = false, decimal unpaid = 0m, params PlanPotResponse[] pots) =>
        new(year, month, free, carriedIn, unpaid, pots);

    // A1, R10: eine Anfrage für die ganze Übersicht
    [Fact]
    public void A10_plan_opens_the_list_of_months_with_one_overview_request()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);
        PlanApiFake.Plans[(2026, 9)] = FakePlanApi.Empty(2026, 9);
        PlanApiFake.Plans[(2026, 8)] = FakePlanApi.Empty(2026, 8);

        var cut = OpenOverview();

        Assert.NotEmpty(cut.FindAll("[data-testid='plan-months']"));
        Assert.Empty(cut.FindAll("[data-testid='month-review']"));
        Assert.Empty(cut.FindAll("[role='tab']"));
        Assert.Equal([2026], PlanApiFake.OverviewCalls);
        Assert.Empty(PlanApiFake.GetCalls);
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
        Assert.All(PlanApiFake.Owners, Assert.Null);
        Assert.Equal(3, RowIds(cut).Count);
    }

    // Ein älterer Server kennt die Übersicht nicht (404): Die Seite lädt wie früher Liste und Monate einzeln, ohne Fehlermeldung.
    [Fact]
    public void Against_a_server_without_the_overview_the_page_falls_back_to_the_per_month_calls()
    {
        PlanApiFake.OverviewMissing = true;
        PlanApiFake.Months = [new PlanMonthResponse(2026, 10, 300m, 50m), new PlanMonthResponse(2026, 9, 20m)];
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with
        {
            Positions = [FakePlanApi.Position("Strom", 80m, booked: 30m)],
            Overview = new PlanOverviewResponse(3000m, 0m, 300m, [new PlanPotResponse(PotKeys.Needs, 80m, 30m)], []),
        };

        var cut = OpenOverview();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-month-unpaid-2026-10']")));
        Assert.Equal(["2026-10", "2026-9"], RowIds(cut));
        Assert.Contains("300,00 €", Row(cut, "2026-10"));
        Assert.Contains("inkl. Übertrag", Row(cut, "2026-10"));
        Assert.Contains("30,00 € von 80,00 € geplant", cut.Find("[data-testid='plan-month-pot-2026-10-needs']").TextContent);
        Assert.Empty(cut.FindAll(AlertSelector));
        Assert.Equal(1, PlanApiFake.ListMonthsCalls);
        Assert.Contains((2026, 10), PlanApiFake.GetCalls);
        Assert.NotEmpty(cut.FindAll("[data-testid='plan-next-month']"));
    }

    // A6: aktueller Monat zuerst, ein Knopf für den nächsten, keine leeren künftigen Karten
    [Fact]
    public void A6_current_month_first_and_a_button_for_the_next()
    {
        PlanApiFake.Overview = [Month(2026, 10, 100m), Month(2026, 9, 50m), Month(2026, 8, 5m)];

        var cut = OpenOverview();

        Assert.Equal(["2026-10", "2026-9", "2026-8"], RowIds(cut));
        var next = cut.Find("[data-testid='plan-next-month']");
        Assert.Contains("Nächsten Monat anlegen", next.TextContent);
        Assert.Equal("/plan/2026/11", next.GetAttribute("href"));
    }

    [Fact]
    public void An_existing_future_month_is_listed_and_the_button_follows_the_last_one()
    {
        PlanApiFake.Overview = [Month(2026, 12, 20m), Month(2026, 10, 100m)];

        var cut = OpenOverview();

        Assert.Equal(["2026-10", "2026-12"], RowIds(cut));
        Assert.Equal("/plan/2027/1", cut.Find("[data-testid='plan-next-month']").GetAttribute("href"));
    }

    [Fact]
    public void Without_a_plan_the_current_month_shows_as_not_created_and_is_the_only_card()
    {
        PlanApiFake.Overview = [];

        var cut = OpenOverview();

        Assert.Equal(["2026-10"], RowIds(cut));
        Assert.Contains("noch nicht angelegt", Row(cut, "2026-10"));
    }

    // A7: das Ziel des Balkens ist die geplante Summe, gefüllt wird mit dem Gebuchten
    [Fact]
    public void A7_the_pot_bar_target_is_the_planned_sum()
    {
        PlanApiFake.Overview =
        [
            Month(2026, 10, 300m, unpaid: 50m,
                pots: [new PlanPotResponse(PotKeys.Needs, 1778m, 1634m), new PlanPotResponse(PotKeys.Wants, 100m, 130m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)]),
        ];

        var cut = OpenOverview();

        var needs = cut.Find("[data-testid='plan-month-pot-2026-10-needs']");
        Assert.Contains("1.634,00 € von 1.778,00 € geplant", needs.TextContent);
        Assert.Contains("Grundbedarf", needs.TextContent);
        Assert.Contains("width: 91.9", needs.InnerHtml);
        Assert.DoesNotContain("%", needs.TextContent);
        Assert.DoesNotContain("überzogen", needs.TextContent);
        var wants = cut.Find("[data-testid='plan-month-pot-2026-10-wants']");
        Assert.Contains("überzogen", wants.TextContent);
        Assert.NotEmpty(wants.QuerySelectorAll(".bar-fill--over-end"));
    }

    // Spec 82: Jede Karte eines angelegten Monats zeigt Verplanbar, Anstehend und drei Topf-Balken.
    [Fact]
    public void A_month_card_shows_allocatable_unpaid_and_three_pot_bars()
    {
        PlanApiFake.Overview = [Month(2026, 10, 300m, unpaid: 50m)];

        var cut = OpenOverview();

        Assert.Contains("300,00 €", cut.Find("[data-testid='plan-month-free-2026-10']").TextContent);
        Assert.Contains("50,00 €", cut.Find("[data-testid='plan-month-unpaid-2026-10']").TextContent);
        Assert.Equal(3, cut.FindAll("[data-testid^='plan-month-pot-2026-10-']").Count);
    }

    // A3
    [Fact]
    public void The_current_month_is_highlighted_and_labelled_as_such()
    {
        PlanApiFake.Overview = [Month(2026, 10, 100m), Month(2026, 9, 50m)];

        var cut = OpenOverview();

        var current = cut.FindAll("[aria-current='date']");
        Assert.Equal("plan-month-row-2026-10", Assert.Single(current).GetAttribute("data-testid"));
        Assert.Contains("aktueller Monat", Row(cut, "2026-10"));
        Assert.DoesNotContain("aktueller Monat", Row(cut, "2026-9"));
    }

    // A4
    [Fact]
    public void A_month_with_a_plan_shows_its_free_amount()
    {
        PlanApiFake.Overview = [Month(2026, 10, 1234.5m), Month(2026, 9, -20m)];

        var cut = OpenOverview();

        Assert.Contains("1.234,50 €", Row(cut, "2026-10"));
        Assert.Contains("Verplanbar", Row(cut, "2026-10"));
        Assert.Contains("-20,00 €", Row(cut, "2026-9"));
    }

    // A11, Spec 66 R9: Die Zahl bricht nie um, der Hinweis steht darunter.
    [Fact]
    public void A11_the_free_figure_does_not_wrap()
    {
        PlanApiFake.Overview = [Month(2026, 10, 6339.37m, carriedIn: true)];

        var cut = OpenOverview();

        var free = cut.Find("[data-testid='plan-month-free-2026-10']");
        Assert.Contains("inkl. Übertrag", free.TextContent);
        var figure = free.QuerySelector(".plan-month-free-figure")!;
        Assert.Contains("6.339,37 €", figure.TextContent);
        Assert.Contains("Verplanbar", figure.TextContent);
        Assert.DoesNotContain("Übertrag", figure.TextContent);
        Assert.NotNull(free.QuerySelector(".plan-month-free-hint"));
        Assert.Matches(@"\.plan-month-free-figure\s*\{[^}]*white-space:\s*nowrap", File.ReadAllText(RepositoryFiles.Path("Shared/Duofy.Shared/wwwroot/duofy.css")));
    }

    // Spec 66, R9: Die Zeile sagt, dass der Übertrag im Betrag steckt, sobald einer da ist.
    [Fact]
    public void A_month_with_a_carry_over_says_so_at_its_free_amount()
    {
        PlanApiFake.Overview = [Month(2026, 10, 530m, carriedIn: true), Month(2026, 9, 300m)];

        var cut = OpenOverview();

        Assert.Contains("530,00 €", Row(cut, "2026-10"));
        Assert.Contains("inkl. Übertrag", cut.Find("[data-testid='plan-month-free-2026-10']").TextContent);
        Assert.DoesNotContain("Übertrag", Row(cut, "2026-9"));
    }

    [Fact]
    public void Earlier_months_load_on_demand_with_the_year_before()
    {
        PlanApiFake.Overview = [Month(2026, 10, 100m)];

        var cut = OpenOverview();
        PlanApiFake.Overview = [Month(2025, 12, 7m), Month(2026, 10, 100m)];
        cut.Find("[data-testid='plan-earlier']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["2026-10", "2025-12"], RowIds(cut)));
        Assert.Equal([2026, 2025], PlanApiFake.OverviewCalls);
    }

    // Review M4: Ein leeres Jahr zwischen zwei Jahren mit Plänen sperrt die älteren nicht aus.
    [Fact]
    public void M4_earlier_months_behind_an_empty_year_are_still_reachable()
    {
        PlanApiFake.OverviewProvider = (year, _) => year switch
        {
            2026 => [Month(2026, 10, 100m)],
            2024 => [Month(2024, 5, 7m)],
            _ => [],
        };

        var cut = OpenOverview();
        cut.Find("[data-testid='plan-earlier']").Click();

        cut.WaitForAssertion(() => Assert.Equal(["2026-10", "2024-5"], RowIds(cut)));
        Assert.Equal([2026, 2025, 2024], PlanApiFake.OverviewCalls);
    }

    [Fact]
    public void M4_the_button_goes_away_when_no_earlier_year_has_plans()
    {
        PlanApiFake.OverviewProvider = (year, _) => year == 2026 ? [Month(2026, 10, 100m)] : [];

        var cut = OpenOverview();
        cut.Find("[data-testid='plan-earlier']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='plan-earlier']")));
        Assert.Equal(["2026-10"], RowIds(cut));
    }

    // Review S1: Ohne Freigabe der Buchungen wäre Anstehend das ganze Geplante; die Monatsseite zeigt dann "–", die Karte auch.
    [Fact]
    public async Task S1_the_card_hides_unpaid_for_a_member_whose_bookings_are_not_shared()
    {
        PlanApiFake.OverviewProvider = (_, _) => [Month(2026, 10, 100m, unpaid: 890m)];
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.BenId);

        var cut = OpenOverview();

        var unpaid = cut.Find("[data-testid='plan-month-unpaid-2026-10']").TextContent;
        Assert.Contains("–", unpaid);
        Assert.DoesNotContain("890", unpaid);
    }

    [Fact]
    public async Task S1_the_card_shows_unpaid_when_the_bookings_are_shared()
    {
        PlanApiFake.OverviewProvider = (_, _) => [Month(2026, 10, 100m, unpaid: 890m)];
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View, Book = AccessLevelKeys.View } },
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.BenId);

        var cut = OpenOverview();

        Assert.Contains("890,00 €", cut.Find("[data-testid='plan-month-unpaid-2026-10']").TextContent);
    }

    private async Task<TaskCompletionSource> HoldFirstOverviewForAnnaAsync()
    {
        var release = new TaskCompletionSource();
        PlanApiFake.OverviewProvider = (_, owner) => owner is null ? [Month(2026, 10, 1m)] : [Month(2026, 9, 2m)];
        PlanApiFake.OverviewGate = (_, owner) => owner is null ? release.Task : null;
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        await Member.LoadAsync();
        return release;
    }

    // Review M6: Die Antwort der alten Sicht trifft nach dem Wechsel ein und darf nichts mehr ändern.
    [Fact]
    public async Task M6_a_late_answer_of_the_previous_view_does_not_mix_into_the_new_one()
    {
        var release = await HoldFirstOverviewForAnnaAsync();
        var cut = Render<PlanOverview>();
        cut.WaitForAssertion(() => Assert.Single(PlanApiFake.OverviewCalls));

        await cut.InvokeAsync(() => Member.Choose(FakeHouseholdApi.BenId));
        cut.WaitForAssertion(() => Assert.Equal(["2026-10", "2026-9"], RowIds(cut)));
        await ReleaseAndAwaitLateAnswerAsync(cut, release);

        Assert.Equal(["2026-10", "2026-9"], RowIds(cut));
        Assert.DoesNotContain("1,00 €", Row(cut, "2026-10"));
    }

    // Wartet auf die verspätete Antwort selbst (die Aufgabe des Fakes) und lässt die Seite danach ihre Fortsetzung abarbeiten.
    private async Task ReleaseAndAwaitLateAnswerAsync(IRenderedComponent<PlanOverview> cut, TaskCompletionSource release)
    {
        var late = PlanApiFake.OverviewTasks.Where(t => !t.IsCompleted).ToList();
        Assert.NotEmpty(late);
        release.SetResult();
        await Task.WhenAll(late);
        await cut.InvokeAsync(() => { });
        cut.Render();
    }

    [Fact]
    public async Task M6_a_late_answer_of_earlier_months_does_not_mix_into_the_new_view()
    {
        var release = new TaskCompletionSource();
        PlanApiFake.OverviewProvider = (year, owner) => (year, owner) switch
        {
            (2026, null) => [Month(2026, 10, 1m)],
            (2025, null) => [Month(2025, 12, 5m)],
            _ => [Month(2026, 9, 2m)],
        };
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        await Member.LoadAsync();
        var cut = OpenOverview();
        PlanApiFake.OverviewGate = (year, owner) => year == 2025 && owner is null ? release.Task : null;

        cut.Find("[data-testid='plan-earlier']").Click();
        cut.WaitForAssertion(() => Assert.Contains(2025, PlanApiFake.OverviewCalls));
        await cut.InvokeAsync(() => Member.Choose(FakeHouseholdApi.BenId));
        cut.WaitForAssertion(() => Assert.Equal(["2026-10", "2026-9"], RowIds(cut)));
        await ReleaseAndAwaitLateAnswerAsync(cut, release);

        Assert.Equal(["2026-10", "2026-9"], RowIds(cut));
        Assert.DoesNotContain("1,00 €", Row(cut, "2026-10"));
    }

    // Review M4: Das Suchen nach früheren Monaten zählt nur Monate des gefragten Jahres; vorhandene künftige Monate späterer Jahre
    // in der Antwort zählen nicht und beenden die Suche nicht.
    [Fact]
    public void M4_future_months_in_the_answer_do_not_end_the_search_for_earlier_months()
    {
        PlanApiFake.OverviewProvider = (year, _) => year switch
        {
            2026 => [Month(2027, 3, 9m), Month(2026, 10, 100m)],
            2025 => [Month(2027, 3, 9m), Month(2026, 10, 100m)],
            2024 => [Month(2027, 3, 9m), Month(2026, 10, 100m), Month(2024, 5, 7m)],
            _ => [Month(2027, 3, 9m), Month(2026, 10, 100m)],
        };

        var cut = OpenOverview();
        cut.Find("[data-testid='plan-earlier']").Click();

        cut.WaitForAssertion(() => Assert.Contains("2024-5", RowIds(cut)));
        Assert.Equal([2026, 2025, 2024], PlanApiFake.OverviewCalls);
    }

    // A5
    [Fact]
    public void Tapping_a_row_opens_that_month_at_its_own_address()
    {
        PlanApiFake.Overview = [Month(2026, 10, 100m), Month(2026, 9, 50m)];

        var cut = OpenOverview();

        Assert.Equal("/plan/2026/9", cut.Find("[data-testid='plan-month-row-2026-9']").GetAttribute("href"));
        Assert.Equal("/plan/2026/10", cut.Find("[data-testid='plan-month-row-2026-10']").GetAttribute("href"));
    }

    // A5
    [Fact]
    public void The_month_address_is_a_route_of_the_plan_page_and_opens_that_month()
    {
        Assert.Equal("/plan/{Year:int}/{Month:int}", typeof(Plan).GetCustomAttributes<RouteAttribute>().Single().Template);
        Assert.NotNull(typeof(Plan).GetCustomAttribute<AuthorizeAttribute>());

        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        Assert.Equal((2026, 9), Assert.Single(PlanApiFake.GetCalls));
        Assert.Equal("September 2026", cut.Find("[data-testid='page-title']").TextContent.Trim());
    }

    [Fact]
    public void Other_parameters_of_the_open_page_load_the_other_month()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 12));

        cut.WaitForAssertion(() => Assert.Equal("Dezember 2026", cut.Find("[data-testid='page-title']").TextContent.Trim()));
        Assert.Equal([(2026, 9), (2026, 12)], PlanApiFake.GetCalls);
    }

    [Fact]
    public void A_month_that_does_not_exist_goes_back_to_the_overview()
    {
        Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 13));

        Assert.Equal("/plan", CurrentPath);
        Assert.Empty(PlanApiFake.GetCalls);
    }

    // Spec 82, R15: Der Monat hat keinen Umschalter oben rechts mehr; er führt zurück zur Übersicht der Monate.
    [Fact]
    public void The_month_has_no_switch_and_leads_back_to_the_months()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        Assert.Empty(cut.FindAll("[data-testid='plan-next'],[data-testid='plan-previous']"));
        Assert.Equal("/plan", cut.Find("[data-testid='plan-back']").GetAttribute("href"));
    }

    // A6
    [Fact]
    public void The_month_plan_has_a_link_back_to_the_overview()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        var back = cut.Find("[data-testid='plan-back']");

        Assert.Equal("/plan", back.GetAttribute("href"));
        Assert.Contains("Alle Pläne", back.TextContent);
    }

    // A6
    [Fact]
    public async Task The_household_month_has_the_link_back_as_well()
    {
        await ShowHouseholdAsync();

        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        Assert.Equal("/plan", cut.Find("[data-testid='plan-back']").GetAttribute("href"));
        Assert.Equal((HouseholdApiFake.Household!.Id, 2026, 9), Assert.Single(HouseholdApiFake.PlanCalls));
    }

    private async Task ShowHouseholdAsync()
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        await Member.LoadAsync();
        Member.ChooseHousehold();
    }

    // R2 für den Haushalt
    [Fact]
    public async Task For_the_household_the_list_comes_from_the_household_plans()
    {
        await ShowHouseholdAsync();
        HouseholdApiFake.PlanMonths.AddRange([new HouseholdPlanMonth(2026, 10, 3110m), new HouseholdPlanMonth(2026, 9, 0m)]);

        var cut = OpenOverview();

        Assert.Equal(["2026-10", "2026-9"], RowIds(cut));
        Assert.Contains("3.110,00 €", Row(cut, "2026-10"));
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
        Assert.Empty(PlanApiFake.OverviewCalls);
        Assert.Empty(cut.FindAll("[data-testid='plan-next-month']"));
        Assert.Contains("Haushalt von Anna", cut.Markup);
    }

    // Spec 68: das Mitglied, dessen Zahlen gezeigt werden
    [Fact]
    public async Task For_a_member_who_shares_the_plan_the_list_asks_for_that_member()
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        OpenOverview();

        Assert.Equal(FakeHouseholdApi.AnnaId, Assert.Single(PlanApiFake.Owners));
        Assert.Equal([2026], PlanApiFake.OverviewCalls);
    }

    [Fact]
    public async Task For_a_member_who_does_not_share_the_plan_the_page_says_so_and_asks_for_nothing()
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Accounts = AccessLevelKeys.View } },
            FakeHouseholdApi.Ben(isMe: true),
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.AnnaId);

        var cut = Render<PlanOverview>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='not-shared']")));
        Assert.Empty(cut.FindAll("[data-testid='plan-months']"));
        Assert.Empty(PlanApiFake.OverviewCalls);
    }

    [Fact]
    public void When_the_list_cannot_be_loaded_the_page_shows_the_error_and_still_offers_the_current_month()
    {
        PlanApiFake.Error = ClientErrorCodes.ServerUnreachable;

        var cut = Render<PlanOverview>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(AlertSelector)));
        Assert.Equal(["2026-10"], RowIds(cut));
    }
}
