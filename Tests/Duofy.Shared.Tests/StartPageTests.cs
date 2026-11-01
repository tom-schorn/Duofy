using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Die Startseite mit Begrüßung, Frei-Betrag, anstehenden Zahlungen und Soll-Ist (Spec 33, A1 bis A11).
/// "Heute" ist der 21. Oktober 2026.
/// </summary>
public class StartPageTests : PageTestContext
{
    private const string NotShared = "Diese Person teilt diesen Bereich nicht mit dir.";

    private readonly IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> dialogs;

    public StartPageTests()
    {
        dialogs = RenderRadzenHost();
        Accounts.Accounts.Add(new AccountChoice(Guid.NewGuid(), "Girokonto", IsDefault: true, Active: true));
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static PlanOverviewResponse Overview(decimal free = 340m, IReadOnlyList<PlanCarryOverLineResponse>? carryOvers = null) =>
        new(3000m, 2660m, free,
            [new PlanPotResponse(PotKeys.Needs, 1000m, 1200m), new PlanPotResponse(PotKeys.Wants, 0m, 0m), new PlanPotResponse(PotKeys.Savings, 0m, 0m)],
            [], carryOvers);

    private void SeedPlan(decimal free = 340m, IReadOnlyList<PlanCarryOverLineResponse>? carryOvers = null, params PlanPositionResponse[] positions) =>
        PlanApiFake.Plans[(2026, 10)] = FakePlanApi.Empty(2026, 10) with { Positions = positions, Overview = Overview(free, carryOvers) };

    private IRenderedComponent<Start> OpenStart()
    {
        var cut = Render<Start>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-hero']")));
        return cut;
    }

    private async Task ChooseBenAsync(GrantLevels levels)
    {
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = levels },
        ]);
        await Member.LoadAsync();
        Member.Choose(FakeHouseholdApi.BenId);
    }

    private static string Text(IRenderedComponent<Start> cut, string testId) => cut.Find($"[data-testid='{testId}']").TextContent;

    // A1 (R1)
    [Fact]
    public void The_hero_block_greets_by_name_and_shows_the_free_amount_of_the_current_month()
    {
        SeedPlan(free: 340m);

        var cut = OpenStart();

        var hero = Text(cut, "start-hero");
        Assert.Contains("Hallo, Anna!", hero);
        Assert.Contains("Frei im Oktober", hero);
        Assert.Contains("340,00 €", hero);
        Assert.DoesNotContain("Hier entsteht bald", cut.Markup);
    }

    // A1 (R1): ein Frei-Betrag von 0 heißt "Alles verplant"
    [Fact]
    public void A_free_amount_of_zero_reads_all_planned()
    {
        SeedPlan(free: 0m);

        var cut = OpenStart();

        Assert.Contains("Alles verplant", Text(cut, "start-hero"));
    }

    // A2 (R1)
    [Fact]
    public void The_hero_block_names_a_carry_over_that_is_in_the_free_amount()
    {
        SeedPlan(free: 390m, carryOvers: [new PlanCarryOverLineResponse(CarryOverDirections.In, 2026, 9, 50m)]);

        var cut = OpenStart();

        Assert.Contains("Enthält Übertrag: 50,00 €", Text(cut, "start-hero"));
    }

    [Fact]
    public void Without_a_carry_over_the_hero_block_has_no_note()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.DoesNotContain("Übertrag", Text(cut, "start-hero"));
    }

    // A3 (R2)
    [Fact]
    public void New_booking_opens_the_booking_dialog()
    {
        SeedPlan();
        var cut = OpenStart();

        cut.Find("[data-testid='start-new-booking']").Click();

        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll(".step-dialog")));
    }

    [Fact]
    public async Task New_booking_is_locked_when_the_person_may_not_book()
    {
        SeedPlan();
        await ChooseBenAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = OpenStart();

        var button = cut.Find("[data-testid='start-new-booking']").Closest("button")!;
        Assert.True(button.HasAttribute("disabled"));
        Assert.Contains("Dafür hat dir Ben keine Freigabe gegeben.", cut.Markup);
    }

    // A4 (R3)
    [Fact]
    public void Without_a_household_a_hint_leads_to_the_household_page()
    {
        SeedPlan();
        HouseholdApiFake.Household = null;

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-household-hint']")));
        Assert.Contains("Mit jemandem zusammen planen?", Text(cut, "start-household-hint"));
        Assert.Equal("/haushalt", cut.Find("[data-testid='start-household-hint-link']").GetAttribute("href"));
    }

    [Fact]
    public void With_a_household_there_is_no_hint()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-household-hint']"));
    }

    // A5 (R4)
    [Fact]
    public void Without_a_plan_for_the_month_the_empty_state_leads_to_the_plan_and_creates_nothing()
    {
        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='start-empty']")));
        Assert.Contains("Für Oktober gibt es noch keinen Plan", Text(cut, "start-empty"));
        Assert.Equal("/plan/2026/10", cut.Find("[data-testid='start-empty-link']").GetAttribute("href"));
        Assert.Contains("Hallo, Anna!", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='start-free']"));
        Assert.Empty(cut.FindAll("[data-testid='start-due']"));
        Assert.Empty(cut.FindAll("[data-testid='start-bars']"));
        Assert.Empty(PlanApiFake.GetCalls);
    }

    // A6 (R5)
    [Fact]
    public void Coming_and_overdue_payments_are_listed_overdue_first_with_their_badge()
    {
        var soon = FakePlanApi.Position("Strom", 80m, day: 23);
        var late = FakePlanApi.Position("Miete", 30m, day: 15);
        SeedPlan(positions: [soon, late]);

        var cut = OpenStart();

        var rows = cut.FindAll("[data-testid^='start-due-item-']");
        Assert.Equal(2, rows.Count);
        Assert.Equal("start-due-item-overdue", rows[0].GetAttribute("data-testid"));
        Assert.Contains("Miete", rows[0].TextContent);
        Assert.Contains("überfällig", rows[0].TextContent);
        Assert.Contains("Strom", rows[1].TextContent);
        Assert.Contains("anstehend", rows[1].TextContent);
    }

    [Fact]
    public void Without_anything_due_the_block_says_so()
    {
        SeedPlan();

        var cut = OpenStart();

        Assert.Contains("In den nächsten Tagen ist nichts fällig.", Text(cut, "start-due"));
    }

    [Fact]
    public void At_most_five_payments_are_shown()
    {
        SeedPlan(positions: Enumerable.Range(0, 7).Select(i => FakePlanApi.Position($"Posten {i}", 10m, day: 22)).ToArray());

        var cut = OpenStart();

        Assert.Equal(5, cut.FindAll("[data-testid^='start-due-item-']").Count);
    }

    // A7 (R6)
    [Fact]
    public void The_target_actual_bars_show_one_bar_per_pot()
    {
        SeedPlan();

        var cut = OpenStart();

        var bars = cut.Find("[data-testid='start-bars']");
        Assert.Equal(3, bars.QuerySelectorAll("figure.chart").Length);
        Assert.Contains("Grundbedarf", bars.TextContent);
        Assert.Contains("Wünsche", bars.TextContent);
        Assert.Contains("Sparen", bars.TextContent);
    }

    // A8 (R7)
    [Fact]
    public void The_blocks_open_the_month_of_the_plan()
    {
        SeedPlan();

        var cut = OpenStart();

        foreach (var id in new[] { "start-hero-link", "start-due-link", "start-bars-link" })
        {
            Assert.Equal("/plan/2026/10", cut.Find($"[data-testid='{id}']").GetAttribute("href"));
        }
    }

    // A9 (R8)
    [Fact]
    public async Task Another_member_who_shares_the_plan_but_not_the_bookings_shows_their_figures_without_payments()
    {
        SeedPlan(free: 120m);
        await ChooseBenAsync(GrantLevels.None with { Plan = AccessLevelKeys.View });

        var cut = OpenStart();

        Assert.Equal(FakeHouseholdApi.BenId, PlanApiFake.Owners.Last());
        Assert.Contains("120,00 €", Text(cut, "start-hero"));
        Assert.Contains(NotShared, Text(cut, "start-due"));
        Assert.Empty(cut.FindAll("[data-testid^='start-due-item-']"));
    }

    // A10 (R8)
    [Fact]
    public async Task Another_member_who_does_not_share_the_plan_shows_the_note_instead_of_the_blocks()
    {
        SeedPlan();
        await ChooseBenAsync(GrantLevels.None with { Accounts = AccessLevelKeys.View });

        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.Contains(NotShared, cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='start-free']"));
        Assert.Empty(PlanApiFake.GetCalls);
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
    }

    // A11 (R8)
    [Fact]
    public async Task The_household_view_shows_the_household_plan_and_offers_no_booking()
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Contains("700,00 €", Text(cut, "start-hero"));
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
        Assert.Empty(cut.FindAll("[data-testid='start-new-booking']"));
        Assert.Equal(0, PlanApiFake.ListMonthsCalls);
    }

    // Der Wechsel im Umschalter lädt die Zahlen der neuen Sicht.
    [Fact]
    public async Task Choosing_another_member_reloads_the_figures()
    {
        SeedPlan(free: 340m);
        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
        [
            FakeHouseholdApi.Anna(),
            FakeHouseholdApi.Ben(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } },
        ]);
        var cut = OpenStart();
        Assert.Contains("340,00 €", Text(cut, "start-hero"));

        Member.Choose(FakeHouseholdApi.BenId);

        cut.WaitForAssertion(() => Assert.Equal(FakeHouseholdApi.BenId, PlanApiFake.Owners.Last()));
    }

    // --- Richtwert-Hinweis (Issue #34) ------------------------------------------------------------------------------

    private static HouseholdMemberResponse GuidelineMember(Guid id, string name, decimal needs, decimal wants, decimal savings) => new(
        id, name, 3000m, true,
        [
            new HouseholdMemberQuotaResponse(PotKeys.Needs, 50, needs, needs - 50m),
            new HouseholdMemberQuotaResponse(PotKeys.Wants, 30, wants, wants - 30m),
            new HouseholdMemberQuotaResponse(PotKeys.Savings, 20, savings, savings - 20m),
        ]);

    private async Task<HouseholdResponse> SeedHouseholdPlanAsync(params HouseholdMemberResponse[] members)
    {
        var household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        HouseholdApiFake.Household = household;
        HouseholdApiFake.PlanMonths.Add(new HouseholdPlanMonth(2026, 10, 700m));
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with { Free = 700m, Members = members };
        await Member.LoadAsync();
        return household;
    }

    [Fact]
    public async Task In_the_household_view_a_person_far_from_the_guideline_gets_a_calm_hint_with_the_name()
    {
        await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 50m, 30m, 20m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 75m, 15m, 10m));
        Member.ChooseHousehold();

        var cut = OpenStart();

        var hint = Text(cut, "start-guideline");
        Assert.Contains("Ben", hint);
        Assert.DoesNotContain("Anna", hint);
        Assert.Contains("Anhaltspunkt", hint);
        Assert.Equal($"/plan/2026/10", cut.Find("[data-testid='start-guideline-link']").GetAttribute("href"));
        Assert.Single(HouseholdApiFake.PlanCalls);
    }

    [Fact]
    public async Task Without_a_deviation_beyond_the_tolerance_there_is_no_hint()
    {
        await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 52m, 28m, 20m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 48m, 32m, 20m));
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
    }

    [Fact]
    public async Task A_person_without_income_gets_no_hint()
    {
        await SeedHouseholdPlanAsync(
            GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 50m, 30m, 20m),
            new HouseholdMemberResponse(FakeHouseholdApi.BenId, "Ben", 0m, false, []));
        Member.ChooseHousehold();

        var cut = OpenStart();

        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
    }

    [Fact]
    public async Task In_the_own_view_of_a_household_member_the_hint_is_there_too()
    {
        SeedPlan(free: 340m);
        var household = await SeedHouseholdPlanAsync(GuidelineMember(FakeHouseholdApi.AnnaId, "Anna", 80m, 10m, 10m), GuidelineMember(FakeHouseholdApi.BenId, "Ben", 50m, 30m, 20m));

        var cut = OpenStart();

        cut.WaitForAssertion(() => Assert.Contains("Anna", Text(cut, "start-guideline")));
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
    }

    [Fact]
    public async Task Without_a_household_plan_for_the_month_or_without_a_household_there_is_no_hint()
    {
        SeedPlan(free: 340m);
        var cut = OpenStart();
        Assert.Empty(cut.FindAll("[data-testid='start-guideline']"));
        Assert.Empty(HouseholdApiFake.PlanCalls);

        HouseholdApiFake.Household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20, [FakeHouseholdApi.Anna(), FakeHouseholdApi.Ben()]);
        await Member.LoadAsync();
        var again = OpenStart();
        Assert.Empty(again.FindAll("[data-testid='start-guideline']"));
        Assert.Empty(HouseholdApiFake.PlanCalls);
    }
}
