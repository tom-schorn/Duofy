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

    // A1
    [Fact]
    public void Plan_opens_the_list_of_months_and_not_the_month_itself()
    {
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10);

        var cut = OpenOverview();

        Assert.NotEmpty(cut.FindAll("[data-testid='plan-months']"));
        Assert.Empty(cut.FindAll("[data-testid='plan-sums']"));
        Assert.Empty(PlanApiFake.GetCalls);
        Assert.Equal(1, PlanApiFake.ListMonthsCalls);
        Assert.Null(Assert.Single(PlanApiFake.Owners));
    }

    // A2
    [Fact]
    public void The_list_has_the_months_with_a_plan_and_the_three_coming_ones_newest_first()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2026, 10, 100m), new PlanMonthResponse(2026, 9, 50m)];

        var cut = OpenOverview();

        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10", "2026-9"], RowIds(cut));
        Assert.Contains("Januar 2027", Row(cut, "2027-1"));
        Assert.Contains("September 2026", Row(cut, "2026-9"));
    }

    [Fact]
    public void An_older_month_with_a_plan_stays_in_the_list_and_the_current_month_shows_without_a_plan()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2025, 12, 10m)];

        var cut = OpenOverview();

        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10", "2025-12"], RowIds(cut));
    }

    // A3
    [Fact]
    public void The_current_month_is_highlighted_and_labelled_as_such()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2026, 10, 100m), new PlanMonthResponse(2026, 9, 50m)];

        var cut = OpenOverview();

        var current = cut.FindAll(".plan-month--current");
        Assert.Equal("plan-month-row-2026-10", Assert.Single(current).GetAttribute("data-testid"));
        Assert.Contains("aktueller Monat", Row(cut, "2026-10"));
        Assert.DoesNotContain("aktueller Monat", Row(cut, "2026-9"));
        Assert.Equal("date", cut.Find("[data-testid='plan-month-row-2026-10']").GetAttribute("aria-current"));
        Assert.Null(cut.Find("[data-testid='plan-month-row-2026-9']").GetAttribute("aria-current"));
    }

    // A4
    [Fact]
    public void A_month_with_a_plan_shows_its_free_amount_and_one_without_says_not_created_yet()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2026, 10, 1234.5m), new PlanMonthResponse(2026, 9, -20m)];

        var cut = OpenOverview();

        Assert.Contains("1.234,50 €", Row(cut, "2026-10"));
        Assert.Contains("Frei", Row(cut, "2026-10"));
        Assert.Contains("-20,00 €", Row(cut, "2026-9"));
        Assert.Contains("noch nicht angelegt", Row(cut, "2026-11"));
        Assert.DoesNotContain("€", Row(cut, "2026-11"));
        Assert.DoesNotContain("noch nicht angelegt", Row(cut, "2026-10"));
    }

    // Spec 66, R9: Die Zeile sagt, dass der Übertrag im Betrag steckt, sobald einer da ist.
    [Fact]
    public void A_month_with_a_carry_over_says_so_at_its_free_amount()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2026, 11, 530m, 230m), new PlanMonthResponse(2026, 10, 300m)];

        var cut = OpenOverview();

        Assert.Contains("530,00 € inkl. Übertrag", Row(cut, "2026-11"));
        Assert.DoesNotContain("Übertrag", Row(cut, "2026-10"));
    }

    // A5
    [Fact]
    public void Tapping_a_row_opens_that_month_at_its_own_address()
    {
        PlanApiFake.Months = [new PlanMonthResponse(2026, 10, 100m), new PlanMonthResponse(2026, 9, 50m)];

        var cut = OpenOverview();

        Assert.Equal("/plan/2026/9", cut.Find("[data-testid='plan-month-row-2026-9']").GetAttribute("href"));
        Assert.Equal("/plan/2027/1", cut.Find("[data-testid='plan-month-row-2027-1']").GetAttribute("href"));
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
        Assert.Equal("September 2026", cut.Find("[data-testid='plan-month']").TextContent.Trim());
    }

    [Fact]
    public void Other_parameters_of_the_open_page_load_the_other_month()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        cut.Render(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 12));

        cut.WaitForAssertion(() => Assert.Equal("Dezember 2026", cut.Find("[data-testid='plan-month']").TextContent.Trim()));
        Assert.Equal([(2026, 9), (2026, 12)], PlanApiFake.GetCalls);
    }

    [Fact]
    public void A_month_that_does_not_exist_goes_back_to_the_overview()
    {
        Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 13));

        Assert.Equal("/plan", CurrentPath);
        Assert.Empty(PlanApiFake.GetCalls);
    }

    [Fact]
    public void Paging_between_the_months_keeps_the_address_in_step()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        cut.Find("[data-testid='plan-next']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Oktober 2026", cut.Find("[data-testid='plan-month']").TextContent.Trim()));
        Assert.Equal("/plan/2026/10", CurrentPath);
    }

    // A6
    [Fact]
    public void The_month_plan_has_a_link_back_to_the_overview()
    {
        var cut = Render<Plan>(p => p.Add(x => x.Year, 2026).Add(x => x.Month, 9));
        OpenPlanTab(cut);

        var back = cut.Find("[data-testid='plan-back']");

        Assert.Equal("/plan", back.GetAttribute("href"));
        Assert.Contains("Alle Monate", back.TextContent);
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

        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10", "2026-9"], RowIds(cut));
        Assert.Contains("3.110,00 €", Row(cut, "2026-10"));
        Assert.Contains("noch nicht angelegt", Row(cut, "2026-11"));
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
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
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
    }

    [Fact]
    public void When_the_list_cannot_be_loaded_the_page_shows_the_error_and_still_offers_the_current_and_coming_months()
    {
        PlanApiFake.Error = ClientErrorCodes.ServerUnreachable;

        var cut = Render<PlanOverview>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(AlertSelector)));
        Assert.Equal(["2027-1", "2026-12", "2026-11", "2026-10"], RowIds(cut));
    }
}
