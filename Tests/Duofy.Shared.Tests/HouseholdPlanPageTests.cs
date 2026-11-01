using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Layout;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>
/// Spec 69, R9 und A10: Der Umschalter bietet "Haushalt", solange der Haushalt mehr als ein Mitglied hat, und die
/// Planseite zeigt dann den Haushaltsplan nur zum Lesen. Angemeldet ist hier Ben im Haushalt von Anna; "heute" ist
/// der 21. Oktober 2026.
/// </summary>
public class HouseholdPlanPageTests : PageTestContext
{
    private const string NoJointView = "Für den Haushalt gibt es hier keine gemeinsame Sicht.";

    private readonly HouseholdResponse household;

    public HouseholdPlanPageTests()
    {
        RenderRadzenHost();
        household = new HouseholdResponse(Guid.NewGuid(), "Haushalt von Anna", 50, 30, 20,
            [FakeHouseholdApi.Anna(isMe: false), FakeHouseholdApi.Ben(isMe: true)]);
        HouseholdApiFake.Household = household;
    }

    private ActiveMember Member => Services.GetRequiredService<ActiveMember>();

    private static string Names(IRenderedComponent<MemberSwitch> cut) =>
        string.Join(",", cut.FindAll("[data-testid^='member-choice-'] .rz-navigation-item-text").Select(o => o.TextContent.Trim()));

    private static IRenderedComponent<MemberSwitch> Open(IRenderedComponent<MemberSwitch> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='member-profile']")));
        cut.Find("[data-testid='member-profile']").Click();
        return cut;
    }

    // R9
    [Fact]
    public void In_a_household_of_two_the_menu_offers_the_household_even_without_any_grant()
    {
        var cut = Open(Render<MemberSwitch>());

        cut.WaitForAssertion(() => Assert.Null(cut.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled")));
        Assert.Equal("Ich,Haushalt", Names(cut));
    }

    // A10, Spec 44 A2: Bei einem Haushalt von einem ist "Haushalt" sichtbar, aber nicht verfügbar.
    [Fact]
    public void In_a_household_of_one_the_household_entry_is_unavailable()
    {
        HouseholdApiFake.Household = household with { Members = [FakeHouseholdApi.Ben(isMe: true)] };

        var cut = Open(Render<MemberSwitch>());

        cut.WaitForAssertion(() => Assert.Equal(1, HouseholdApiFake.MineCalls));
        Assert.Equal("true", cut.Find("[data-testid='member-choice-household']").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Granted_members_and_the_household_are_both_offered_and_choosing_the_household_clears_the_owner()
    {
        HouseholdApiFake.Household = household with
        {
            Members = [FakeHouseholdApi.Anna(isMe: false) with { GrantsToMe = GrantLevels.None with { Plan = AccessLevelKeys.View } }, FakeHouseholdApi.Ben(isMe: true)],
        };
        var cut = Open(Render<MemberSwitch>());
        cut.WaitForAssertion(() => Assert.Equal("Ich,Anna,Haushalt", Names(cut)));
        cut.Find($"[data-testid='member-choice-{FakeHouseholdApi.AnnaId}']").Click();

        cut.Find("[data-testid='member-profile']").Click();
        cut.Find("[data-testid='member-choice-household']").Click();

        Assert.True(Member.IsHousehold);
        Assert.Null(Member.Owner);
        Assert.False(Member.Allows(GrantAreas.Plan, AccessLevelKeys.View));
        Assert.Equal("Haushalt", cut.Find("[data-testid='member-profile']").TextContent.Trim());
    }

    [Fact]
    public async Task Choosing_a_person_or_me_leaves_the_household_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        Member.Choose(null);

        Assert.False(Member.IsHousehold);
        Assert.True(Member.Allows(GrantAreas.Plan, AccessLevelKeys.Delete));
    }

    // A10: Haushaltsname, Posten mit Besitzer, keine Knöpfe zum Ändern.
    [Fact]
    public async Task The_plan_page_shows_the_household_plan_with_owner_names_and_no_buttons_to_change_anything()
    {
        var rent = FakeHouseholdApi.OwnedPosition("Miete", 890m, FakeHouseholdApi.AnnaId, "Anna");
        var power = FakeHouseholdApi.OwnedPosition("Strom", 80m, FakeHouseholdApi.BenId, "Ben", booked: 30m);
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Positions = [rent, power],
            Pots = [new HouseholdPotResponse(PotKeys.Needs, 970m, 30m), new HouseholdPotResponse(PotKeys.Wants, 0m, 0m), new HouseholdPotResponse(PotKeys.Savings, 0m, 0m)],
            Income = 3000m,
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"[data-testid='household-position-{rent.Id}']")));

        Assert.Equal("Haushalt von Anna", cut.Find("[data-testid='household-name']").TextContent.Trim());
        Assert.Contains("Anna", cut.Find($"[data-testid='household-position-{rent.Id}']").TextContent);
        Assert.Contains("Ben", cut.Find($"[data-testid='household-position-{power.Id}']").TextContent);
        Assert.Contains("890,00 € geplant", cut.Find($"[data-testid='household-position-{rent.Id}']").TextContent);
        Assert.Contains("30,00 € gebucht", cut.Find($"[data-testid='household-position-{power.Id}']").TextContent);
        Assert.Contains("Oktober 2026", cut.Find("[data-testid='plan-month']").TextContent);
        Assert.Equal((household.Id, 2026, 10), Assert.Single(HouseholdApiFake.PlanCalls));
        Assert.Empty(PlanApiFake.GetCalls);
        foreach (var gone in new[] { "plan-add", "plan-carry-over-open", "plan-changes" })
        {
            Assert.Empty(cut.FindAll($"[data-testid='{gone}']"));
        }
        Assert.Empty(cut.FindAll("input[type='checkbox']"));
        Assert.Empty(cut.FindAll("button[data-testid^='plan-edit'],button[data-testid^='plan-delete']"));
    }

    [Fact]
    public async Task The_household_plan_pages_between_the_months()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();
        var cut = Render<Plan>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-month']")));

        cut.Find("[data-testid='plan-next']").Click();

        cut.WaitForAssertion(() => Assert.Contains("November 2026", cut.Find("[data-testid='plan-month']").TextContent));
        Assert.Equal([(household.Id, 2026, 10), (household.Id, 2026, 11)], HouseholdApiFake.PlanCalls);
    }

    // R8
    [Fact]
    public async Task Accounts_are_shown_for_those_who_granted_them_and_the_others_are_named()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Accounts = [new HouseholdAccountResponse(Guid.NewGuid(), FakeHouseholdApi.AnnaId, "Anna", "Giro", true, 1234.50m)],
            AccountsMissing = ["Clara"],
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='household-accounts']")));

        Assert.Contains("Giro", cut.Find("[data-testid='household-accounts']").TextContent);
        Assert.Contains("1.234,50 €", cut.Find("[data-testid='household-accounts']").TextContent);
        Assert.Equal("Ohne Freigabe der Konten fehlen hier: Clara.", cut.Find("[data-testid='household-accounts-missing']").TextContent.Trim());
    }

    [Fact]
    public async Task Without_missing_accounts_there_is_no_notice()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='plan-month']")));

        Assert.Empty(cut.FindAll("[data-testid='household-accounts-missing']"));
    }

    // R4: Summen und Quoten wie bei einer Person.
    [Fact]
    public async Task The_sums_and_quotas_of_the_household_are_shown()
    {
        HouseholdApiFake.HouseholdPlans[(2026, 10)] = FakeHouseholdApi.EmptyPlan(household.Id, household.Name, 2026, 10) with
        {
            Income = 5000m,
            Unpaid = 120m,
            Quotas = [new PlanQuotaResponse(PotKeys.Needs, 50, 50.0m), new PlanQuotaResponse(PotKeys.Wants, 30, 0m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
            Unplanned = new HouseholdUnplannedResponse(0m, 0m, 40m, 0m),
        };
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();
        OpenPlanTab(cut);

        var sums = cut.Find("[data-testid='plan-sums']").TextContent;
        Assert.Contains("5.000,00 €", sums);
        Assert.Contains("120,00 €", sums);
        Assert.Contains("40,00 €", cut.Find("[data-testid='household-unplanned']").TextContent);
        Assert.Contains("Ziel 50 %, Ist 50 %", cut.Find("[data-testid='plan-quota-needs']").TextContent);
    }

    [Fact]
    public async Task A_failing_household_plan_shows_the_error()
    {
        HouseholdApiFake.Error = ErrorCodes.HouseholdNotFound;
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var cut = Render<Plan>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(AlertSelector)));
    }

    // R9: Auf den anderen Seiten gibt es keine gemeinsame Sicht.
    [Fact]
    public async Task Other_pages_say_there_is_no_joint_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        var bookings = Render<Bookings>();
        var accounts = Render<Accounts>();
        var commitments = Render<Commitments>();

        bookings.WaitForAssertion(() => Assert.Equal(NoJointView, bookings.Find("[data-testid='not-shared']").TextContent.Trim()));
        accounts.WaitForAssertion(() => Assert.Equal(NoJointView, accounts.Find("[data-testid='not-shared']").TextContent.Trim()));
        commitments.WaitForAssertion(() => Assert.Equal(NoJointView, commitments.Find("[data-testid='not-shared']").TextContent.Trim()));
        Assert.Empty(BookingApiFake.ListOwners);
    }

    [Fact]
    public async Task Leaving_the_household_of_two_drops_the_household_view()
    {
        await Member.LoadAsync();
        Member.ChooseHousehold();

        HouseholdApiFake.Household = household with { Members = [FakeHouseholdApi.Ben(isMe: true)] };
        await Member.RefreshAsync();

        Assert.False(Member.IsHousehold);
        Assert.False(Member.CanShowHousehold);
    }
}
